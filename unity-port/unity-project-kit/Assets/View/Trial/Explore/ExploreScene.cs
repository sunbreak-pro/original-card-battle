// The exploration scene (port of src/scenes/explore/ExploreScene.js; dungeon_exploration_v4 §6; explore.md). It renders
// ExplorationScreenBuilder's view-model and forwards the player's input to the ported ExplorationSession / Expedition.
// It computes no rule and no number: every value and line comes from the bridge (or the scene-text table,
// ExploreText). What it adds is presentation: the per-layer backdrop under its veil and the miasma shade, the map drawn
// on it, a walker that travels each step, number roll-ups, the cards over the map, the battle hand-off and the reward
// pick, the trace pick, the interlude, and keyboard support. Trial mode (docs/EXTENSIONS.md 「探索の持ち物とイベント」)
// adds the tools with their notes, consumable slots that act (標の石 aims on the map), the swap card, the event cards and
// their result, 写しの帳's survey pick, and 刻限の砂 / 防瘴の面 in the band.
//
// ExploreScene.Mount(host, ctx) → IScene { Destroy(), Refresh() }: see ExploreContracts.cs for the ctx. The router
// destroys this scene to show a battle and mounts a fresh one afterwards (main.js:390, 398); a scene mounted while the
// encounter waits on Return fades in (is-returning) and brings it back (proceed → returnFlow).
//
// This file is the scene core: mount / destroy / refresh, the DOM's layer tree and z-order, the clock, render, the key
// legend and the hint, the scene's keys, the overlays and lifeEnd. The shared state and helpers are
// ExploreScene.State.cs; the other sections are the other partial files (see there).
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using Exploration;
using Journal;
using TMPro;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Explore
{
    public sealed partial class ExploreScene
    {
        // js: scenes/explore/ExploreScene.js:181
        /// <summary>mountExploreScene(host, ctx): builds the scene into host (a stage layer) and starts it.</summary>
        public static IScene Mount(RectTransform host, ExploreSceneCtx ctx)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            var scene = new ExploreScene(host, ctx);
            scene.Start();
            return scene;
        }

        private ExploreScene(RectTransform host, ExploreSceneCtx ctx)
        {
            Ctx = ctx;
            Expedition = ctx.Expedition ?? throw new ArgumentException("ctx.Expedition is required", nameof(ctx));
            Session = Expedition.Session;
            Audio = ctx.Audio;
            Settings = ctx.Settings ?? TrialSettings.Defaults();
            Reduce = ctx.ReducedMotion ?? CssTime.ReducedMotion;
            Host = host;
        }

        /// <summary>The scene's keys (ExploreScene.js:2285 popKeys).</summary>
        private Action PopKeys;

        private void Start()
        {
            // ---- a button chosen with Tab acts on Enter / Space (ExploreScene.js:197-246; the kit's Focus) ----
            Scope = Focus.PushScope("explore");
            Focus.Changed += OnFocusChanged;

            // ---- the clock (ExploreScene.js:400) ----
            ClockDriver driver = Ctx.Stage ? Ctx.Stage.Clocks : CssTime.Driver;
            Clock = driver.CreateClock();
            Clock.OnFrame((dtScene, dtReal) => OnClockFrame(Clock.Now));

            // ---- DOM (ExploreScene.js:249-323) ----
            BuildDom();

            // ---- input (ExploreScene.js:2269-2324) ----
            EndLifeBtn.Click += EndLifeClick;
            PopKeys = KeysWithFocus(OnSceneKey);

            // ---- start (ExploreScene.js:2326-2335) ----
            // Back from the app's battle scene (the encounter waits on Return): the map fades back in where it was, with
            // no layer banner. Otherwise the layer assembles row by row under its banner.
            bool returning = Expedition.Battle != null && Expedition.Battle.Over;
            if (returning)
            {
                SetRootClass(".is-returning", true);
                CssAnimator.Play(Root, ExploreStyle.ReturningAnim);
            }
            Render(enter: !returning);
            StartFlow(returning).Forget();
        }

        // js: scenes/explore/ExploreScene.js:2332
        private async Promise StartFlow(bool returning)
        {
            if (!returning && (Session.State.Phase == RunPhase.Exploring || Session.HasPendingBattle)) await LayerBanner();
            Proceed().Forget();
        }

        // ------------------------------------------------------------------ DOM

        /// <summary>The DOM of ExploreScene.js:249-283: the root and its layers in DOM order, sorted by z-index (UiNode.Z).</summary>
        // js: scenes/explore/ExploreScene.js:249
        private void BuildDom()
        {
            Root = Ui.Div(Host, "ocbg-explore", 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH);
            UiNode.Of(Root);
            BoxGraphic.On(Root, ExploreStyle.RootCss);

            BdHost = Layer("ocbg-explore-bdhost", ExploreStyle.ZBackdrop);

            MapLayer = Layer("ocbg-explore-map", ExploreStyle.ZMap);
            UiNode.Of(MapLayer).PointerEvents = PointerEvents.None;

            Banner = Ui.Div(Root, "ocbg-explore-banner");
            UiNode.Of(Banner).Z = ExploreStyle.ZBanner;
            UiNode.Of(Banner).PointerEvents = PointerEvents.None;

            Detail = Ui.Div(Root, "ocbg-explore-detail", ExploreStyle.DetailX, ExploreStyle.DetailY, ExploreStyle.DetailW, ExploreStyle.DetailH);
            UiNode.Of(Detail).Z = ExploreStyle.ZDetail;

            Lull = Layer("ocbg-explore-lullview", ExploreStyle.ZLull);
            UiNode.Of(Lull).PointerEvents = PointerEvents.None;

            CardLayer = Layer("ocbg-explore-cards", ExploreStyle.ZCards);
            UiNode.Of(CardLayer).PointerEvents = PointerEvents.None;

            Carried = Ui.Div(Root, "ocbg-explore-carried", ExploreStyle.CarriedX, ExploreStyle.CarriedY, ExploreStyle.CarriedW, ExploreStyle.CarriedH);
            UiNode.Of(Carried).Z = ExploreStyle.ZCarried;

            // js: scenes/explore/ExploreScene.js:276
            EndLifeBtn = OcbgButton.Make(Root, ExplorationText.EndLifeButton, false, false, "ocbg-explore-endlife");
            EndLifeBtn.States.State(".ocbg-explore-endlife", ExploreStyle.EndLifeCss, true);
            EndLifeBtn.States.Apply(instant: true);
            EndLifeBtn.SetBox(ExploreStyle.EndLifeX, ExploreStyle.EndLifeY, ExploreStyle.EndLifeW, ExploreStyle.EndLifeH);
            EndLifeBtn.Node.Z = ExploreStyle.ZEndLife;
            AddFocus(EndLifeBtn);

            // js: scenes/explore/ExploreScene.js:277
            Hint = Ui.Div(Root, "ocbg-explore-hint", ExploreStyle.HintX, ExploreStyle.HintY, ExploreStyle.HintW, ExploreStyle.HintH);
            UiNode.Of(Hint).Z = ExploreStyle.ZHint;
            HintStyle = RichStyle(TextStyle.FromCss(ExploreStyle.HintCss + ExploreStyle.HintSpanCss));
            HintLabel = Label.Make(Hint, "", HintStyle, 0f, 0f, ExploreStyle.HintW, null, "hint");
            TextGlow.For(HintLabel);

            // js: scenes/explore/ExploreScene.js:279 (right-aligned from the right edge; a long key list reaches left)
            Legend = Ui.Div(Root, "ocbg-explore-legend", ExploreStyle.LegendX, ExploreStyle.LegendY, ExploreStyle.LegendW, ExploreStyle.LegendH);
            UiNode.Of(Legend).Z = ExploreStyle.ZLegend;
            UiNode.Of(Legend).PointerEvents = PointerEvents.None;

            Band = Ui.Div(Root, "ocbg-explore-band");
            UiNode.Of(Band).Z = ExploreStyle.ZBand;

            OverlayLayer = Layer("ocbg-explore-overlays", ExploreStyle.ZOverlays);
            UiNode.Of(OverlayLayer).PointerEvents = PointerEvents.None;

            // js: scenes/explore/ExploreScene.js:283
            Backdrop = ExploreBackdrop.Create(BdHost);

            // the parts build their elements into the layers
            BuildMapLayer();
            BuildBanner();
            BuildDetail();
            BuildCarried();
            BuildBand();

            // The root's classes that reach the core's layers (the parts register their own in their Build methods).
            // explore.css:68 / :70 / :370: the map fades and dims; :370 the detail fades with the interlude.
            CssStates map = CssStates.Of(MapLayer).Base(ExploreStyle.MapCss);
            map.Apply(instant: true);
            WatchRootClass(".is-engaging", map, ExploreStyle.MapEngagingCss);
            WatchRootClass(".is-interlude", map, "opacity: 0;");
            CssStates detail = CssStates.Of(Detail).SetTransitions(ExploreStyle.DetailTransition);
            WatchRootClass(".is-interlude", detail, "opacity: 0;");
            WatchRootClass(".is-interlude", on => UiNode.Of(Detail).PointerEvents = on ? PointerEvents.None : PointerEvents.Inherit);
        }

        /// <summary>A full-stage child layer of the root (position: absolute; inset: 0) with its z-index.</summary>
        private RectTransform Layer(string name, int z)
        {
            RectTransform rt = Ui.Div(Root, name, 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH);
            UiNode.Of(rt).Z = z;
            return rt;
        }

        // ------------------------------------------------------------------ the clock

        // js: scenes/explore/ExploreScene.js:400
        private void OnClockFrame(float now)
        {
            if (InBattle || !Alive) return;
            Backdrop?.Frame(now);
            foreach (RollLabel r in Rollers.ToArray()) r.Tick(now);
            PaintGauge(now);
            WalkerAnim?.Invoke(now);
        }

        // ------------------------------------------------------------------ the key legend and the hint

        private TextStyle HintStyle;
        private TMP_Text HintLabel;

        /// <summary>The legend's items as laid out (their widths feed FitHint: the JS reads offsetWidth).</summary>
        private readonly List<float> LegendWidths = new List<float>();

        private static TextStyle _legendText, _kbdText, _kbdLegendText;

        private static TextStyle LegendText => _legendText ?? (_legendText = TextStyle.FromCss(ExploreStyle.LegendCss));

        /// <summary>kbd's own font (explore.css:11), centred on one line.</summary>
        private static TextStyle KbdText => _kbdText ?? (_kbdText = KbdStyle(""));

        /// <summary>A kbd of the key legend: its text also takes the legend's inherited text-shadow (explore.css:214).</summary>
        private static TextStyle KbdLegendText => _kbdLegendText ?? (_kbdLegendText = KbdStyle("text-shadow: 0 0 6px var(--ground);"));

        private static TextStyle KbdStyle(string inherited)
        {
            TextStyle s = TextStyle.FromCss(ExploreStyle.KbdCss + inherited);
            s.Align = TextAlign.Center;
            s.WhiteSpace = WhiteSpace.NoWrap;
            return s;
        }

        // (named for this file: every part of the partial class may reset its own statics)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCoreStatics()
        {
            _legendText = null;
            _kbdText = null;
            _kbdLegendText = null;
        }

        // js: scenes/explore/ExploreScene.js:209
        /// <summary>redrawLegend(): the legend again, as it stands (a Tab-focused button names its Enter there).</summary>
        private void RedrawLegend()
        {
            try { if (Alive && Legend) DrawLegend(LegendNodeCard); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // js: scenes/explore/ExploreScene.js:205 (onFocusIn / onFocusOut / onPointerCapture: the focus changed)
        private void OnFocusChanged() => RedrawLegend();

        /// <summary>DrawLegend is running / was asked again meanwhile (a focus change it caused: Focus.Changed → RedrawLegend).</summary>
        private bool _legendDrawing, _legendAgain;

        // js: scenes/explore/ExploreScene.js:327
        /// <summary>drawLegend(node): the keys of what can be done now (labels from the bridge where it has them).</summary>
        private void DrawLegend(NodeCard node)
        {
            LegendNodeCard = node;
            // Reading the focus may blur a button that stopped being focusable, which redraws the legend from inside this
            // draw: that redraw runs after this one instead of adding a second set of items to the half-built legend.
            if (_legendDrawing)
            {
                _legendAgain = true;
                return;
            }
            _legendDrawing = true;
            try
            {
                do
                {
                    _legendAgain = false;
                    DrawLegendOnce(LegendNodeCard);
                }
                while (_legendAgain && Alive && Legend);
            }
            finally
            {
                _legendDrawing = false;
            }
        }

        private void DrawLegendOnce(NodeCard node)
        {
            // A button Tab moved the focus to takes Enter and Space first (X-8): the legend names that button for Enter,
            // and leaves out the screen's own Enter / Space, which would not act.
            Focusable focused = FocusedButton(Root);
            Ui.Clear(Legend);
            LegendWidths.Clear();
            var items = new List<LegendEntry>();
            void Key(string label, params string[] keys) => items.Add(new LegendEntry(label, keys));

            if (focused != null) Key(ButtonLabel(focused), ExploreText.KeyEnter);
            bool PressKeys(IReadOnlyList<string> keys) => keys.Any(k => k == ExploreText.KeyEnter || k == "Space");
            if (Plate != null && Plate.Legend != null)
            {
                // a card over the map lists its own keys
                foreach (LegendEntry e in Plate.Legend)
                    if (focused == null || !PressKeys(e.Keys)) items.Add(e);
            }
            else if (focused != null)
            {
                bool live = Screen != null && Screen.MapIsLive && !Busy && Plate == null;
                if (live) Key(ExploreText.KeySelect, "←→↑↓");
                if (live && Screen.Aiming) Key(ExploreText.StopAim, "Esc");
            }
            else
            {
                bool live = Screen != null && Screen.MapIsLive && !Busy && Plate == null;
                if (live) Key(ExploreText.KeySelect, "←→↑↓");
                if (live && node != null)
                {
                    for (int i = 0; i < node.Options.Count; i++)
                    {
                        if (i == 0) Key(node.Options[i].Label, ExploreText.KeyEnter);
                        else Key(node.Options[i].Label, ExploreText.KeyShift, ExploreText.KeyEnter);
                    }
                }
                if (live && Screen.Aiming) Key(ExploreText.StopAim, "Esc");
                if (Screen != null && Plate == null && !Busy && Session.State.Phase == RunPhase.Interlude && Screen.Card.Buttons.Count > 0)
                    Key(Screen.Card.Buttons[0].Label, ExploreText.KeyEnter);
            }
            if (Screen != null && Screen.Consumables.Any(c => c.CanUse) && (Plate == null || Plate.Kind == ExploreStyle.PlateScreen))
                Key(ExploreText.KeyItems, "1–3");

            LayOutLegend(items);
            FitHint();
        }

        /// <summary>
        /// The legend's spans in a row (gap 20), right-aligned in its box (justify-content: flex-end; a row wider than the
        /// box runs on past its left edge), each vertically centred (align-items: center): kbd … kbd label, gap 6.
        /// </summary>
        private void LayOutLegend(List<LegendEntry> items)
        {
            TextStyle text = LegendText;
            var widths = new List<float>();
            foreach (LegendEntry e in items)
            {
                float w = 0f;
                foreach (string k in e.Keys) w += KbdWidth(k) + ExploreStyle.LegendItemGap;
                w += TextMeasure.Width(e.Label, text);
                if (string.IsNullOrEmpty(e.Label) && e.Keys.Count > 0) w -= ExploreStyle.LegendItemGap;
                widths.Add(w);
            }
            float total = widths.Sum() + Math.Max(0, items.Count - 1) * ExploreStyle.LegendGap;
            float x = ExploreStyle.LegendW - total;
            const float itemH = 24f;
            float y0 = (ExploreStyle.LegendH - itemH) / 2f;
            for (int i = 0; i < items.Count; i++)
            {
                LegendEntry e = items[i];
                RectTransform span = Ui.Div(Legend, "key", x, y0, widths[i], itemH);
                float cx = 0f;
                foreach (string k in e.Keys)
                {
                    float kw = KbdWidth(k);
                    Kbd(span, k, cx, 0f, kw, inLegend: true);
                    cx += kw + ExploreStyle.LegendItemGap;
                }
                if (!string.IsNullOrEmpty(e.Label))
                {
                    TMP_Text l = Label.Make(span, e.Label, text, cx, (itemH - text.LineHeightPx) / 2f, null, null, "label");
                    TextGlow.For(l);
                }
                LegendWidths.Add((float)Math.Round(widths[i], MidpointRounding.AwayFromZero));
                x += widths[i] + ExploreStyle.LegendGap;
            }
        }

        /// <summary>
        /// A kbd's border-box width (border-box: bundle.css:3): its text + padding 0 6 + border 1, at least `minW` (24; the
        /// slots' kbd says 22, explore.css:189).
        /// </summary>
        internal static float KbdWidth(string key, float minW = 24f) => Math.Max(minW, TextMeasure.Width(key, KbdText) + 12f + 2f);

        /// <summary>
        /// The scene's kbd (explore.css:11) at (x, y) of parent: an `h` px tall key box (24; the option label's and the
        /// slots' are 22, explore.css:159 / :189) with its key centred. Only the key legend's kbd takes a text-shadow (the
        /// legend's, inherited: explore.css:214); nowhere else does a kbd sit under one.
        /// </summary>
        internal static RectTransform Kbd(RectTransform parent, string key, float x, float y, float? w = null, float h = 24f, bool inLegend = false)
        {
            TextStyle kbd = inLegend ? KbdLegendText : KbdText;
            float kw = w ?? KbdWidth(key);
            BoxGraphic box = BoxGraphic.Make(parent, ExploreStyle.KbdCss, x, y, kw, h, "kbd");
            RectTransform rt = box.rectTransform;
            // content box: h − 2 tall inside the 1 px border; the 16 px line (font: 600 16px/1) centred
            TMP_Text l = Label.Make(rt, key, kbd, 1f, 1f + (h - 2f - kbd.LineHeightPx) / 2f, kw - 2f, kbd.LineHeightPx, "key");
            if (inLegend) TextGlow.For(l);
            return rt;
        }

        // js: scenes/explore/ExploreScene.js:360
        /// <summary>
        /// The hint runs from 「この生を終える」 up to where the key legend's keys start (the legend is right-aligned and its
        /// width follows what can be done now), so a long hint — 標の石's aim — keeps to one line where there is room.
        /// </summary>
        private void FitHint()
        {
            float used = LegendWidths.Sum() + Math.Max(0, LegendWidths.Count - 1) * ExploreStyle.LegendGap;
            float w = Math.Max(ExploreStyle.HintW, ExploreStyle.LegendRight - used - ExploreStyle.HintGap - ExploreStyle.HintX);
            UiNode.Of(Hint).W = w;
            LayOutHint();
        }

        /// <summary>The hint's span in its box: as wide as the box, balanced, vertically centred (display: flex; align-items: center).</summary>
        private void LayOutHint()
        {
            if (!HintLabel) return;
            float w = UiNode.Of(Hint).W;
            LabelState st = Label.StateOf(HintLabel);
            // hint.textContent = '' leaves no span; TMP lays text out only while active, so show it before measuring
            bool has = !string.IsNullOrEmpty(st.Text);
            HintLabel.gameObject.SetActive(has);
            if (!has) return;
            st.AutoW = false;
            st.AutoH = true;
            Ui.Size(HintLabel.rectTransform, w, HintLabel.rectTransform.sizeDelta.y);
            Label.Relayout(HintLabel);
            float h = HintLabel.rectTransform.sizeDelta.y;
            Ui.Place(HintLabel.rectTransform, 0f, (ExploreStyle.HintH - h) / 2f);
        }

        // js: scenes/explore/ExploreScene.js:743
        private void DrawHint()
        {
            // the map's hint speaks of the map: under a card that waits on the player it steps back (the card has its keys)
            string text = !string.IsNullOrEmpty(HintOverride) ? HintOverride
                : Screen != null && !(Plate != null && Plate.Kind != ExploreStyle.PlateScreen) ? Screen.Hint : "";
            if (HintLabel) Label.SetText(HintLabel, string.IsNullOrEmpty(text) ? "" : Rich(text, HintStyle));
            FitHint();
        }

        // ------------------------------------------------------------------ render

        // js: scenes/explore/ExploreScene.js:794
        /// <summary>render({ enter, animate }): the screen again from the session (numbers roll unless animate is false).</summary>
        private void Render(bool enter = false, bool? animate = null)
        {
            if (!Alive) return;
            float now = Now;
            bool anim = !FirstRender && animate != false;
            Screen = Session.Screen();
            ExplorationState state = Session.State;
            int layer = state.Profile.Layer;

            if (state.CurrentNodeId != LastCurrent)
            {
                LastCurrent = state.CurrentNodeId;
                Pinned = state.CurrentNodeId;
            }
            if (Screen.Node(Pinned) == null) Pinned = Screen.CurrentNodeId;

            if (layer != LastLayer) Backdrop.Show(layer, now);
            bool mapChanged = !ReferenceEquals(state.Map, MapRef);
            if (mapChanged)
            {
                MapRef = state.Map;
                BuildMap(Screen, !FirstRender || enter);
                if (WalkerAnim == null) PlaceWalkerOnCurrent();
            }
            LastLayer = layer;

            DrawBand(now, anim);
            UpdateMap(Screen);
            if (WalkerAnim == null && !mapChanged) PlaceWalkerOnCurrent();
            DrawCarried(); // first: it may take height from the detail panel (FitCarried)
            DrawDetail();
            DrawHint();
            EndLifeBtn.Node.Visible = Screen.ShowsEndLife;
            SyncEndLife();

            // the interlude replaces the map (§6.5); the backdrop rests under a warm lull
            bool interlude = state.Phase == RunPhase.Interlude;
            SetRootClass(".is-interlude", interlude);
            Backdrop.SetLull(interlude);
            object amb = interlude ? (object)"interlude" : layer;
            if (!Equals(amb, LastAmbience))
            {
                LastAmbience = amb;
                Ambience(amb);
            }
            DrawInterlude(interlude);

            SetRootClass(".is-busy", Busy);
            DrawScreenCard();
            FirstRender = false;
        }

        private void PlaceWalkerOnCurrent()
        {
            NodeCard cur = Screen.Node(Screen.CurrentNodeId);
            if (cur == null) return;
            Vector2 p = NodePos(cur);
            SetWalker(p.x, p.y);
        }

        // ------------------------------------------------------------------ input

        // js: scenes/explore/ExploreScene.js:2279
        private void EndLifeClick()
        {
            if (Screen == null || !Screen.CanEndLife || Busy) return;
            if (Plate != null && Plate.Kind != ExploreStyle.PlateScreen) return;
            OnCardAction(ScreenAction.AskEndLife).Forget();
        }

        // js: scenes/explore/ExploreScene.js:2285
        /// <summary>The scene's keys (explore.md §5.19); a Tab-focused button's Enter / Space comes first (KeysWithFocus).</summary>
        private bool OnSceneKey(KeyEvent e)
        {
            if (!Alive || Screen == null) return false;
            if (e.CtrlKey || e.MetaKey || e.AltKey) return false;
            string k = e.Key.Length == 1 ? e.Key.ToLowerInvariant() : e.Key;
            // A held key repeats: only a fresh press opens an overlay (the app's overlays close on a fresh press only, so a
            // held J, C or Esc no longer opens and closes them over and over).
            if (e.Repeat && (k == "j" || k == "c" || k == "Escape")) return true;
            if (k == "j")
            {
                if (!Session.HasPendingBattle || !Busy) OpenOverlay(ExploreOverlayKind.Journal).Forget();
                return true;
            }
            if (Busy) return false;
            if (k == "c")
            {
                OpenOverlay(ExploreOverlayKind.Cards).Forget();
                return true;
            }
            if (k == "Escape" && Session.IsAiming && Plate == null && !ConfirmOpen)
            {
                // trial: Esc stops aiming a 標の石 before it opens anything (like a confirmation, §6.2)
                if (Session.Escape())
                {
                    Sfx("uiCancel");
                    Persist();
                    Render();
                }
                return true;
            }
            if (k == "Escape")
            {
                OpenOverlay(ExploreOverlayKind.Settings).Forget();
                return true;
            }
            if (ConfirmOpen) return false;
            // the consumables stay usable while the layer-break card is up and at the interlude (§6.2)
            if ((k == "1" || k == "2" || k == "3") && (Plate == null || Plate.Kind == ExploreStyle.PlateScreen))
            {
                // a key presses the slot's button, so it does nothing where the button is disabled
                int at = k[0] - '1';
                SlotCard slot = at < Screen.Consumables.Count ? Screen.Consumables[at] : null;
                if (slot != null && (slot.CanUse || slot.Aiming)) UseConsumable(slot.Index);
                return true;
            }
            if (Plate != null) return false;
            if (Session.State.Phase == RunPhase.Interlude && (k == "Enter" || k == " "))
            {
                CardButton first = Screen.Card.Buttons.Count > 0 ? Screen.Card.Buttons[0] : null;
                if (first != null) OnCardAction(first.Action).Forget();
                return true;
            }
            if (!Screen.MapIsLive) return false;
            switch (k)
            {
                case "ArrowLeft": MoveSelection(-1, 0); return true;
                case "ArrowRight": MoveSelection(1, 0); return true;
                case "ArrowUp": MoveSelection(0, -1); return true;
                case "ArrowDown": MoveSelection(0, 1); return true;
            }
            if (k == "Enter" || k == " ")
            {
                NodeCard node = Screen.Node(Pinned);
                if (node != null && node.Options.Count > 0) TakeOption(node, e.ShiftKey && node.Options.Count > 1 ? 1 : 0);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ overlays (the app mounts them)

        // js: scenes/explore/ExploreScene.js:2194
        /// <summary>openOverlay(kind): 'journal' | 'cards' | 'settings' through ctx.onOverlay (the fallback without it).</summary>
        private async Promise OpenOverlay(string kind)
        {
            if (Screen == null || OverlayOpen != null) return;
            if (kind == ExploreOverlayKind.Cards && !Screen.CanOpenCards) return;
            if (kind == ExploreOverlayKind.Settings && !Screen.CanOpenSettings) return;
            if (kind == ExploreOverlayKind.Journal) Sfx("journalOpen"); else Sfx("uiClick");
            OverlayOpen = kind;
            try
            {
                if (Ctx.OnOverlay != null)
                {
                    var info = new ExploreOverlayInfo
                    {
                        Book = Screen.Journal,
                        Page = kind == ExploreOverlayKind.Cards ? Screen.Cards : kind == ExploreOverlayKind.Settings ? Screen.Settings : null,
                        Screen = Screen,
                    };
                    Promise r = Ctx.OnOverlay(kind, info);
                    if (r != null)
                    {
                        await r;
                        if (kind == ExploreOverlayKind.Journal) Sfx("journalClose");
                    }
                }
                else
                {
                    await FallbackOverlay(kind);
                    if (kind == ExploreOverlayKind.Journal) Sfx("journalClose");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[explore] overlay " + e);
            }
            finally
            {
                OverlayOpen = null;
            }
            if (Alive) Render();
        }

        // js: scenes/explore/ExploreScene.js:2220
        /// <summary>
        /// Stand-ins for the app's overlays (a dev path for a missing ctx.onOverlay): the builder's OverlayPage, and a plain
        /// journal drawer. Resolves when it closes (its key or its button; destroy closes it too).
        /// </summary>
        private Promise FallbackOverlay(string kind)
        {
            var done = new PromiseSource();
            RectTransform shade = null;
            RectTransform el = null;
            Action pop = null;
            bool isJournal = kind == ExploreOverlayKind.Journal;

            void Close()
            {
                if (OverlayClose == null) return;
                OverlayClose = null;
                pop?.Invoke();
                RectTransform gone = el;
                if (gone) CssAnimator.Play(gone, isJournal ? ExploreStyle.JournalOutAnim : ExploreStyle.OvPageOutAnim);
                if (shade) Ui.Remove(shade);
                Timeout(220f, () => { if (gone) Ui.Remove(gone); });
                done.TryResolve();
            }
            OverlayClose = Close;

            if (isJournal)
            {
                JournalBook book = Screen.Journal;
                el = FallbackJournal(book, Close);
                CssAnimator.Play(el, ExploreStyle.JournalInAnim);
            }
            else
            {
                OverlayPage page = kind == ExploreOverlayKind.Cards ? Screen.Cards : Screen.Settings;
                shade = Ui.Div(OverlayLayer, "ocbg-explore-ovshade", 0f, 0f, ExploreStyle.StageW, ExploreStyle.StageH);
                BoxGraphic sb = BoxGraphic.On(shade, ExploreStyle.OvShadeCss);
                sb.raycastTarget = true;
                UiNode.Of(shade).PointerEvents = PointerEvents.Auto;
                CssAnimator.Play(shade, ExploreStyle.OvShadeAnim);
                el = FallbackPage(page, kind == ExploreOverlayKind.Settings && Ctx.OnSaveQuit != null, Close);
                CssAnimator.Play(el, ExploreStyle.OvPageInAnim);
            }

            string keyOf = kind == ExploreOverlayKind.Journal ? "j" : kind == ExploreOverlayKind.Cards ? "c" : "Escape";
            pop = KeyStack.Push(e =>
            {
                string k = e.Key.Length == 1 ? e.Key.ToLowerInvariant() : e.Key;
                if (k == keyOf || e.Key == "Escape")
                {
                    Close();
                    return true;
                }
                return kind != ExploreOverlayKind.Journal;
            });
            return done.Promise;
        }

        /// <summary>The fallback journal drawer (explore.css:376-391): 576 wide on the right, washi, its tabs and the page.</summary>
        private RectTransform FallbackJournal(JournalBook book, Action close)
        {
            const float w = 576f, padX = 32f, padY = 28f, gap = 14f;
            float innerW = w - 2f * padX;
            RectTransform el = Ui.Div(OverlayLayer, "ocbg-explore-journal", ExploreStyle.StageW - w, 0f, w, ExploreStyle.StageH);
            BoxGraphic bg = BoxGraphic.On(el, ExploreStyle.JournalCss);
            bg.raycastTarget = true;
            UiNode.Of(el).PointerEvents = PointerEvents.Auto;

            // .ocbg-explore-jhead: h2 + the close button (space-between, centred)
            Rgba ink = ExploreStyle.WashiInk;
            TextStyle h2 = TextStyle.FromCss(ExploreStyle.JHeadH2Css).WithColor(ink);
            OcbgButton closeBtn = OcbgButton.Make(el, JournalBook.CloseLabel, false, false, "close");
            closeBtn.States.State(".jhead", ExploreStyle.JHeadBtnCss, true);
            closeBtn.States.Apply(instant: true);
            closeBtn.Layout();
            Vector2 bs = closeBtn.NaturalSize;
            float headH = Math.Max(h2.LineHeightPx, bs.y);
            Label.Make(el, JournalBook.Heading, h2, padX, padY + (headH - h2.LineHeightPx) / 2f, null, null, "h2");
            closeBtn.SetBox(w - padX - bs.x, padY + (headH - bs.y) / 2f, bs.x, bs.y);
            closeBtn.Click += () => close();
            AddFocus(closeBtn);

            // .ocbg-explore-jtabs: wrapping buttons, gap 6
            float y = padY + headH + gap;
            TextStyle tabText = TextStyle.FromCss(ExploreStyle.JTabCss).WithWhiteSpace(WhiteSpace.NoWrap);
            var tabs = new List<(JournalPage Page, BoxGraphic Box, TMP_Text Text, bool Enabled)>();
            float tx = 0f, ty = 0f, rowH = 0f;
            foreach (JournalPage p in book.Pages)
            {
                string t = JournalBook.TabText(p);
                float tw = TextMeasure.Width(t, tabText) + 28f + 2f, th = tabText.LineHeightPx + 12f + 2f;
                if (tx > 0f && tx + tw > innerW) { tx = 0f; ty += rowH + 6f; rowH = 0f; }
                BoxGraphic tab = BoxGraphic.Make(el, ExploreStyle.JTabCss, padX + tx, y + ty, tw, th, "tab");
                TMP_Text tl = Label.Make(tab.rectTransform, t, tabText, 15f, 7f, null, null, "label");
                bool enabled = book.CanOpen(p);
                tabs.Add((p, tab, tl, enabled));
                tx += tw + 6f;
                rowH = Math.Max(rowH, th);
            }
            float tabsH = book.Pages.Count > 0 ? ty + rowH : 0f;

            // .ocbg-explore-jbody: the page (flex: 1, overflow: auto)
            float bodyY = y + tabsH + gap;
            ScrollArea body = ScrollArea.Make(el, "ocbg-explore-jbody", padX, bodyY, innerW, ExploreStyle.StageH - padY - bodyY);

            void Show(JournalPage p)
            {
                foreach (var t in tabs)
                {
                    bool on = t.Page.Id == p.Id;
                    t.Box.SetCss(ExploreStyle.JTabCss + (on ? ExploreStyle.JTabOnCss : "") + (t.Enabled ? "" : ExploreStyle.JTabDisabledCss));
                    Label.SetColor(t.Text, on ? ExploreStyle.Ink : ink);
                    UiNode.Of(t.Box).Opacity = t.Enabled ? 1f : 0.45f;
                }
                Ui.Clear(body.Content);
                float cy = 0f, lastMargin = 0f;
                void Block(string text, TextStyle s, float top, float bottom, string name)
                {
                    cy += Math.Max(lastMargin, top);
                    TMP_Text l = Label.Make(body.Content, text, s, 0f, cy, innerW, null, name);
                    cy += l.rectTransform.sizeDelta.y;
                    lastMargin = bottom;
                }
                // h3: the title and its 開示度 (display: flex; align-items: baseline; justify-content: space-between): the
                // two items' first baselines line up, the higher one's box starts at the h3's top
                TextStyle h3 = TextStyle.FromCss(ExploreStyle.JBodyH3Css).WithColor(ink);
                cy += 6f;
                float h3Base = FirstBaseline(h3), h3H = h3.LineHeightPx;
                if (!string.IsNullOrEmpty(p.DisclosureLabel))
                {
                    TextStyle small = TextStyle.FromCss(ExploreStyle.JBodyH3SmallCss).WithColor(ink).WithAlign(TextAlign.Right);
                    float smallBase = FirstBaseline(small), line = Math.Max(h3Base, smallBase);
                    Label.Make(body.Content, p.Title, h3, 0f, cy + line - h3Base, null, null, "h3");
                    Label.Make(body.Content, p.DisclosureLabel, small, 0f, cy + line - smallBase, innerW, null, "small");
                    h3H = Math.Max(line - h3Base + h3.LineHeightPx, line - smallBase + small.LineHeightPx);
                }
                else Label.Make(body.Content, p.Title, h3, 0f, cy, null, null, "h3");
                cy += h3H;
                lastMargin = 12f;
                TextStyle h4 = TextStyle.FromCss(ExploreStyle.JBodyH4Css).WithColor(ink);
                TextStyle para = TextStyle.FromCss(ExploreStyle.JBodyPCss).WithColor(ink);
                foreach (JournalRow row in p.Rows)
                {
                    if (!string.IsNullOrEmpty(row.Label)) Block(row.Label, h4, 14f, 2f, "h4");
                    if (!string.IsNullOrEmpty(row.Text)) Block(p.TextOf(row), para, 0f, 6f, "p");
                }
                cy += lastMargin;
                body.Content.sizeDelta = new Vector2(innerW, cy);
                body.ScrollTop = 0f;
                body.Refresh();
            }
            foreach (var t in tabs)
            {
                var tab = t;
                if (!tab.Enabled) continue;
                ClickArea.On(tab.Box).Click += _ => Show(tab.Page);
                AddFocus(tab.Box.rectTransform, () => Show(tab.Page), null, null, () => JournalBook.TabText(tab.Page));
            }
            JournalPage first = book.First;
            if (first != null) Show(first);
            return el;
        }

        /// <summary>Where CSS puts a line's first baseline under its box's top: the half-leading plus the face's ascent.</summary>
        private static float FirstBaseline(TextStyle s)
        {
            FontMetrics m = TrialFonts.MetricsOf(s.Family, s.Weight);
            return TextMath.HalfLeading(s.LineHeightPx, s.Size, m.AscentEm, m.DescentEm) + m.AscentEm * s.Size;
        }

        /// <summary>The fallback 所持カード / 設定 page (explore.css:392-394): 1060 × 780 at 430, 150, its lines and its buttons.</summary>
        private RectTransform FallbackPage(OverlayPage page, bool saveQuit, Action close)
        {
            const float w = 1060f, h = 780f, padX = 44f, padY = 36f, gap = 18f;
            float innerW = w - 2f * padX;
            RectTransform el = Ui.Div(OverlayLayer, "ocbg-explore-ovpage", 430f, 150f, w, h);
            BoxGraphic bg = BoxGraphic.On(el, ExploreStyle.OvPageCss);
            bg.raycastTarget = true;
            UiNode.Of(el).PointerEvents = PointerEvents.Auto;

            TextStyle h2 = TextStyle.FromCss(ExploreStyle.JHeadH2Css);
            OcbgButton closeBtn = OcbgButton.Make(el, page.CloseLabel, false, false, "close");
            closeBtn.States.State(".jhead", ExploreStyle.JHeadBtnCss, true);
            closeBtn.States.Apply(instant: true);
            closeBtn.Layout();
            Vector2 bs = closeBtn.NaturalSize;
            float headH = Math.Max(h2.LineHeightPx, bs.y);
            Label.Make(el, page.Title, h2, padX, padY + (headH - h2.LineHeightPx) / 2f, null, null, "h2");
            closeBtn.SetBox(w - padX - bs.x, padY + (headH - bs.y) / 2f, bs.x, bs.y);
            closeBtn.Click += () => close();
            AddFocus(closeBtn);

            // .ocbg-explore-ovlines p: margin 0 0 8; an empty line keeps its height (ExploreScene.js:2256: U+00A0, which does not collapse)
            TextStyle para = TextStyle.FromCss(ExploreStyle.OvLinesPCss);
            float y = padY + headH + gap;
            foreach (string line in page.Lines)
            {
                TMP_Text l = Label.Make(el, string.IsNullOrEmpty(line) ? " " : line, para, padX, y, innerW, null, "p");
                y += l.rectTransform.sizeDelta.y + 8f;
            }

            if (saveQuit)
            {
                // .ocbg-explore-plate-buttons: margin-top auto (the bottom), padding-top 10, right-aligned; min-width 220
                OcbgButton sq = OcbgButton.Make(el, ExploreText.SaveQuit, false, false, "save-quit");
                Vector2 ns = sq.NaturalSize;
                float bw = Math.Max(220f, ns.x);
                sq.SetBox(w - padX - bw, h - padY - ns.y, bw, ns.y);
                sq.Click += () =>
                {
                    close();
                    Ctx.OnSaveQuit?.Invoke();
                };
                AddFocus(sq);
            }
            return el;
        }

        // ------------------------------------------------------------------ lifeEnd

        // js: scenes/explore/ExploreScene.js:165
        /// <summary>
        /// What ctx.onLifeEnd receives: the phase the life ended in (RunPhase: MiasmaDeath / Fallen / Completed /
        /// Survived), the end card ExplorationScreenBuilder wrote, and where it happened.
        /// </summary>
        private static ExploreLifeEnd LifeEnd(RecordedSession session, ExplorationScreen screen)
        {
            ExplorationState state = session.State;
            MapNode node = state.Map.Node(state.CurrentNodeId);
            return new ExploreLifeEnd
            {
                Ending = state.Phase,
                Card = screen.Card,
                Title = screen.Card.Title,
                Lines = screen.Card.Lines,
                Layer = state.Profile.Layer,
                LayerTitle = screen.LayerTitle,
                NodeId = node.Id,
                NodeKind = node.Kind,
                State = state,
            };
        }

        // ------------------------------------------------------------------ IScene

        // js: scenes/explore/ExploreScene.js:2338
        /// <summary>
        /// destroy(): answers an open confirmation (cancel) and closes a fallback overlay so their keys leave the stack,
        /// pops the keys, stops the clock (its timers never fire) and removes the scene.
        /// </summary>
        public void Destroy()
        {
            if (!Alive && !Root) return;
            List<ConfirmCardView> confirms = Root ? global::Trial.Kit.Stage.ConfirmsIn(Root) : new List<ConfirmCardView>();
            Alive = false;
            if (confirms.Count > 0) confirms[0].Cancel();
            OverlayClose?.Invoke();
            PopKeys?.Invoke();
            PopKeys = null;
            Focus.Changed -= OnFocusChanged;
            if (Plate != null) Plate.Pop?.Invoke();
            Scope?.Dispose();
            Clock?.Stop(resolvePending: false);
            WalkerAnim = null;
            try { Backdrop?.Destroy(); }
            catch (Exception e) { Debug.LogException(e); }
            if (Root) Ui.Remove(Root);
        }

        // js: scenes/explore/ExploreScene.js:2354
        /// <summary>refresh(): render(); proceed(); (the router calls it after an overlay over the scene closed).</summary>
        public void Refresh()
        {
            if (!Alive) return;
            Render();
            Proceed().Forget();
        }
    }
}
