// The battle screen's chrome (docs/BATTLE_SCENE.md §6c, battle-views §3.7): the corner tag (CornerInfoView: turn, layer,
// chain dots, miasma, each with its tip), the speed switch (BattleSpeedButton), the journal button, the settings gear (only
// with settingsInBattle), the turn banners, the result paper (a win's / a loss's result screen), the chain banner and the
// loss ink. Layers hud / float / over. Port of scenes/battle/hud/chrome.js.
//
// The CSS strings below are hud.css's rules (and the DS bundle.css ones they sit on, Ocb.*Css) copied verbatim, composed in
// cascade order (later declarations win). Every timed change runs on the scene clock (env.Clock) with the JS's ms and ease.
using System;
using System.Collections.Generic;
using System.Globalization;
using Depiction;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Battle
{
    // js: scenes/battle/hud/chrome.js
    public static class HudChrome
    {
        /// <summary>
        /// CornerTips (scene addition): what each of the corner's items is, on hover, in a line under the tag. The numbers are
        /// the frame's and the app's; the miasma's effect is Miasma's (concept-v3 §6).
        /// </summary>
        // js: scenes/battle/hud/chrome.js:18
        public static class CornerTips
        {
            public static string Turn(string n) => "ターン " + n;
            public static string Layer(int n) => "第 " + n.ToString(CultureInfo.InvariantCulture) + " 層";
            public static string Chain(int index, int total) =>
                "連戦：" + total.ToString(CultureInfo.InvariantCulture) + " 戦のうち " + index.ToString(CultureInfo.InvariantCulture) + " 戦目";
            /// <summary>「瘴気 n%：探索で溜まった瘴気。StepPercent% ごとに最大スタミナ −1（−MaxPenalty まで）」 (Miasma's numbers, DungeonCore's constants).</summary>
            public static string Miasma(float pct) =>
                "瘴気 " + Ocb.Num(pct) + "%：探索で溜まった瘴気。"
                + global::DungeonCore.Miasma.StepPercent.ToString(CultureInfo.InvariantCulture) + "% ごとに最大スタミナ −1（−"
                + global::DungeonCore.Miasma.MaxPenalty.ToString(CultureInfo.InvariantCulture) + " まで）";
        }

        /// <summary>JOURNAL_TIP: the journal button's tooltip (the DS JournalButton's title and its key). scene text</summary>
        // js: scenes/battle/hud/chrome.js:26
        public const string JournalTip = "手記（J）";

        /// <summary>
        /// GEAR_PATH: the settings gear in 付録 A's line style (the exploration screen's gear) — the same string as
        /// Trial.Geometry's ExtraIcons.GearPath, which draws it.
        /// </summary>
        // js: scenes/battle/hud/chrome.js:196
        internal const string GearPath = ExtraIcons.GearPath;

        /// <summary>SETTINGS_TIP: ExplorationText.SettingsButton (設定（Esc）).</summary>
        // js: scenes/battle/hud/chrome.js:197
        public static string SettingsTip => global::Exploration.ExplorationText.SettingsButton;

        /// <summary>SETTINGS_BOX: 56×56 in the journal button's look, 12 px left of the speed switch.</summary>
        // js: scenes/battle/hud/chrome.js:198
        public static readonly ScreenBox SettingsBox = new ScreenBox(
            CornerLayout.SpeedButton.X - CornerLayout.JournalGap - CornerLayout.JournalButton.Width,
            CornerLayout.JournalButton.Y, CornerLayout.JournalButton.Width, CornerLayout.JournalButton.Height);

        /// <summary>
        /// placeUnder(node, box): a tooltip 8 px under a corner button, right edges aligned (CornerLayout.SpeedTooltip): the
        /// line's span measured, the tip given SpeedTooltip's box (border-box), the span centred in it as the flex box centres
        /// it (an item wider than the content box overflows both sides equally).
        /// </summary>
        // js: scenes/battle/hud/chrome.js:103
        internal static void PlaceUnder(RectTransform node, ScreenBox box)
        {
            if (!node) return;
            RectTransform span = node.childCount > 0 ? node.GetChild(0) as RectTransform : null;
            float spanW = span ? span.rect.width : 0f, spanH = span ? span.rect.height : 0f;
            float textW = Mathf.Ceil(span ? spanW : 0f);
            float textH = Mathf.Ceil(span ? (spanH > 0f ? spanH : 22f) : 22f);
            ScreenBox tipBox = CornerLayout.SpeedTooltip(textW, textH);
            float right = box.X + box.Width;
            UiNode n = UiNode.Of(node);
            n.Place(right - tipBox.Width, box.Y + box.Height + CornerLayout.SpeedTooltipGap, tipBox.Width, tipBox.Height);
            if (!span) return;
            BoxStyle s = Ocb.BoxOf(HudUtil.LineTipCss);
            Insets bw = s.BorderWidths, pad = s.Padding;
            float cw = tipBox.Width - bw.Horizontal - pad.Horizontal, ch = tipBox.Height - bw.Vertical - pad.Vertical;
            Ui.Place(span, bw.Left + pad.Left + (cw - spanW) * 0.5f, bw.Top + pad.Top + (ch - spanH) * 0.5f);
        }

        /// <summary>cornerTip(text) = lineTip(text).</summary>
        // js: scenes/battle/hud/chrome.js:114
        internal static RectTransform CornerTip(string text) => HudUtil.LineTip(text);

        /// <summary>
        /// playBanner(env, text, side, ms) = DepictionPlayer.Banner: the DS band at y 400 (132 tall, display 64, amber rules for
        /// the player's turn, violet for the enemy's), faded in over the first quarter and out over the last (linear) of
        /// `ms`, in layer.float; removed at the end.
        /// </summary>
        // js: scenes/battle/hud/chrome.js:247
        public static Promise PlayBanner(BattleViewEnv env, string text, BannerSide side, float ms)
        {
            OcbElement el = Ocb.Banner(env.Layer.Float, text ?? "", side == BannerSide.Enemy);
            el.Root.name = "ocb-banner ocbg-battle-banner";
            UiNode node = el.Node;
            node.Y = L.Banner.Y;
            node.Opacity = 0f;
            return env.Clock.Run(ms, Ease.Linear, t =>
            {
                if (!node) return;
                node.Opacity = Fixed(t < 0.25f ? t / 0.25f : t > 0.75f ? (1f - t) / 0.25f : 1f, 3);
            }).Then(() => el.Remove());
        }

        /// <summary>Number.prototype.toFixed(digits) read back as a number (the style string the JS writes).</summary>
        internal static float Fixed(float x, int digits) => (float)Math.Round((double)x, digits, MidpointRounding.AwayFromZero);
    }

    /// <summary>createCorner(env, chainOpt, tip) → { el, bind, destroy }.</summary>
    // js: scenes/battle/hud/chrome.js:36
    public sealed class HudCorner
    {
        /// <summary>CHAIN_DOT: one 12 px circle per fight of the chain.</summary>
        // js: scenes/battle/hud/chrome.js:35
        private const float ChainDot = 12f;

        /// <summary>hud.css:142 .ocbg-battle-corner.ocb-corner.</summary>
        private const string CornerHudCss = "position: absolute; box-sizing: border-box; color: var(--ink);";
        /// <summary>hud.css:143 .ocbg-battle-corner b (font: inherit) with the b's .ocb-mono.</summary>
        private const string CornerBCss = "font-variant-numeric: tabular-nums;";
        /// <summary>
        /// hud.css:145 .ocbg-battle-corner-chain. The DS's `.ocb-root .ocb-corner span` (0,2,1) outranks it (0,2,0), so the
        /// DS's declarations follow it here: the dots stand 6 px apart, as the browser draws them.
        /// </summary>
        private const string ChainCss = "display: inline-flex; align-items: center; gap: var(--space-4);";
        /// <summary>hud.css:146 .ocbg-battle-chain-dot.</summary>
        private const string ChainDotCss = "display: block; box-sizing: border-box; border-radius: 50%; border: var(--stroke-frame) solid var(--ink-muted);";
        /// <summary>hud.css:147 .ocbg-battle-chain-dot.is-done.</summary>
        private const string ChainDotDoneCss = "background: var(--ink); border-color: var(--ink);";

        private static readonly string ElCss = Ocb.PanelCss + Ocb.CornerCss + CornerHudCss;

        private readonly BattleViewEnv _env;
        private readonly BattleChain _chainOpt;
        private readonly HudTip _tip;
        private readonly RectTransform _el;
        private readonly TextMeshProUGUI _turn, _floor, _miasma;
        private readonly RectTransform _turnItem, _floorItem, _chainItem, _miasmaItem;
        /// <summary>words: what each item says on hover (turn, floor, chain, miasma).</summary>
        private readonly Dictionary<string, string> _words = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["turn"] = "", ["floor"] = "", ["chain"] = "", ["miasma"] = "",
        };
        private string _chainKey = "";

        // js: scenes/battle/hud/chrome.js:36
        private HudCorner(BattleViewEnv env, BattleChain chain, HudTip tip)
        {
            _env = env;
            _chainOpt = chain;
            _tip = tip;
            RectTransform hud = env.Layer.Hud;
            _el = Ocb.El(hud, "ocb-panel ocb-corner ocbg-battle-corner", ElCss);
            TextStyle bs = Ocb.TextOf(ElCss + Ocb.CornerSpanCss + Ocb.MonoCss + CornerBCss);
            _turnItem = Item("turn", "hourglass", DsTokens.Ink, bs, out _turn);
            _floorItem = Item("floor", "stairs", DsTokens.Ink, bs, out _floor);
            _chainItem = Ocb.El(_el, "ocbg-battle-corner-chain", ChainCss + Ocb.CornerSpanCss);
            _miasmaItem = Item("miasma", "miasma", DsTokens.Phosphor, bs, out _miasma);
            Hover(_turnItem, "turn");
            Hover(_floorItem, "floor");
            Hover(_chainItem, "chain");
            Hover(_miasmaItem, "miasma");
            UiNode.Of(_el).Place(24f, CornerLayout.CornerInfoTop, null, CornerLayout.CornerInfoHeight);
            Layout();
        }

        private RectTransform Item(string key, string icon, Rgba color, TextStyle bs, out TextMeshProUGUI text)
        {
            RectTransform span = Ocb.El(_el, key, Ocb.CornerSpanCss);
            Ocb.Icon(span, icon, 22f, color);
            text = Ocb.Text(span, "", bs, "b");
            return span;
        }

        // Each item says what it is on hover (CornerTips), in a line under the tag, left edge on the item's.
        // js: scenes/battle/hud/chrome.js:49
        private void Hover(RectTransform item, string key)
        {
            if (_tip == null) return;
            // .ocbg-battle-hit .ocbg-battle-corner-item: the item takes the pointer
            ClickArea area = ClickArea.On(item);
            BattleHitTag.Mark(item);
            area.Enter += _ =>
            {
                if (string.IsNullOrEmpty(_words[key])) return;
                _tip.Show(HudChrome.CornerTip(_words[key]), n =>
                {
                    // el.offsetLeft + item.offsetLeft: an offsetLeft counts from the offsetParent's padding edge, so the
                    // item's offset inside the tag leaves out the tag's own 1 px left border (UiNode.X counts from the border edge).
                    UiNode el = UiNode.Of(_el), it = UiNode.Of(item);
                    float itemOffsetLeft = it.X - Ocb.BoxOf(ElCss).BorderWidths.Left;
                    UiNode.Of(n).Place(el.X + itemOffsetLeft, el.Y + el.H + CornerLayout.SpeedTooltipGap);
                }, this);
            };
            area.Exit += _ => _tip.Hide(this);
        }

        /// <summary>createCorner(env, chainOpt, tip): the corner tag at (24, 20), 44 tall, in layer.hud.</summary>
        // js: scenes/battle/hud/chrome.js:36
        public static HudCorner Create(BattleViewEnv env, BattleChain chain, HudTip tip = null) => new HudCorner(env, chain, tip);

        /// <summary>el.</summary>
        // js: scenes/battle/hud/chrome.js:71
        public RectTransform El => _el ? _el : null;

        /// <summary>el.offsetLeft + el.offsetWidth: the tag's right edge (the player's stance list's high place stands 12 px right of it).</summary>
        // js: scenes/battle/hud.js:219
        public float Right => _el ? UiNode.Of(_el).X + UiNode.Of(_el).W : 24f + 232f;

        /// <summary>bind(corner): turn, the layer (env.LayerNo), the chain's dots (the app's, else the frame's; ● for fights won), miasma %.</summary>
        // js: scenes/battle/hud/chrome.js:72
        public void Bind(CornerFrame corner)
        {
            if (!_el) return;
            CornerFrame c = corner ?? new CornerFrame();
            Label.SetText(_turn, c.Turn.ToString(CultureInfo.InvariantCulture));
            Label.SetText(_floor, _env.LayerNo.ToString(CultureInfo.InvariantCulture));
            // The chain: the app's (ctx.chain), else the frame's. ● for the fights won (i < the one being fought), ○ for the rest.
            bool app = _chainOpt != null && _chainOpt.Total != 0;
            int index = app ? _chainOpt.Index : c.ChainIndex;
            int total = app ? _chainOpt.Total : c.ChainTotal;
            UiNode.Of(_chainItem).Visible = total > 0;
            string key = index.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture);
            if (key != _chainKey)
            {
                _chainKey = key;
                Ui.Clear(_chainItem);
                for (int i = 1; i <= total; i++)
                {
                    string css = ChainDotCss + (i < index ? ChainDotDoneCss : "");
                    RectTransform dot = Ocb.El(_chainItem, "ocbg-battle-chain-dot" + (i < index ? " is-done" : ""), css);
                    UiNode.Of(dot).Place(0f, 0f, ChainDot, ChainDot);
                    Ocb.NoShrink(dot);
                }
            }
            float pct = _env.MiasmaPercent ?? c.MiasmaPercent;
            Label.SetText(_miasma, Ocb.Num(pct) + "%");
            _words["turn"] = HudChrome.CornerTips.Turn(c.Turn.ToString(CultureInfo.InvariantCulture));
            _words["floor"] = HudChrome.CornerTips.Layer(_env.LayerNo);
            _words["chain"] = total > 0 ? HudChrome.CornerTips.Chain(index, total) : "";
            _words["miasma"] = HudChrome.CornerTips.Miasma(pct);
            Layout();
        }

        /// <summary>The inline-flex row laid out again (the numbers changed their widths).</summary>
        private void Layout()
        {
            Ocb.Flow(_turnItem, Ocb.CornerSpanCss);
            Ocb.Flow(_floorItem, Ocb.CornerSpanCss);
            if (_chainItem.gameObject.activeSelf) Ocb.Flow(_chainItem, ChainCss + Ocb.CornerSpanCss);
            Ocb.Flow(_miasmaItem, Ocb.CornerSpanCss);
            Ocb.Flow(_el, ElCss, null, null, CornerLayout.CornerInfoHeight);
        }

        // js: scenes/battle/hud/chrome.js:98
        public void Destroy()
        {
            _tip?.Hide(this);
            if (_el) Ui.Remove(_el);
        }
    }

    /// <summary>createSpeedButton(env, onSpeed, tip) → { show, setVisible, destroy }.</summary>
    // js: scenes/battle/hud/chrome.js:123
    public sealed class HudSpeedButton
    {
        /// <summary>hud.css:148 .ocbg-battle-speed.</summary>
        private const string SpeedHudCss =
            "position: absolute; box-sizing: border-box; display: flex; align-items: center; justify-content: flex-start; "
            + "font: 600 20px/1 var(--font-mono); color: var(--ink); cursor: pointer; white-space: nowrap;";
        /// <summary>hud.css:149 .ocbg-battle-speed:hover.</summary>
        private const string SpeedHudHoverCss =
            "border-color: var(--ink); background-image: linear-gradient(color-mix(in srgb, var(--ink) 10%, transparent), "
            + "color-mix(in srgb, var(--ink) 10%, transparent));";
        /// <summary>The inline style: paddingLeft CornerLayout.SpeedPadding, gap CornerLayout.SpeedIconGap.</summary>
        private static readonly string InlineCss =
            "padding-left: " + Ocb.Num(CornerLayout.SpeedPadding) + "px; gap: " + Ocb.Num(CornerLayout.SpeedIconGap) + "px;";

        private readonly BattleViewEnv _env;
        private readonly HudTip _tip;
        private readonly RectTransform _el;
        private readonly RectTransform _iconBox;
        private readonly TextMeshProUGUI _label;
        private bool _hover;

        // js: scenes/battle/hud/chrome.js:123
        private HudSpeedButton(BattleViewEnv env, Action onSpeed, HudTip tip)
        {
            _env = env;
            _tip = tip;
            ScreenBox box = CornerLayout.SpeedButton;
            _el = Ocb.El(env.Layer.Hud, "ocb-panel ocbg-battle-speed ocbg-battle-hit", Css(false));
            _iconBox = Ui.Div(_el, "ocbg-battle-speed-icon");
            UiNode.Of(_iconBox).Place(0f, 0f, CornerLayout.SpeedIconSize, CornerLayout.SpeedIconSize);
            Ocb.NoShrink(_iconBox);
            _label = Ocb.Text(_el, "", Ocb.TextOf(Css(false)), "ocbg-battle-speed-label");
            UiNode.Of(_el).Place(box.X, box.Y, box.Width, box.Height);
            ClickArea area = ClickArea.On(_el);
            BattleHitTag.Mark(_el);
            area.HoverChanged += on =>
            {
                _hover = on;
                if (_el) Ocb.Restyle(_el, Css(_hover));
            };
            area.Click += _ => onSpeed?.Invoke();
            area.Enter += _ => _tip?.Show(HudChrome.CornerTip(BattleSpeed.Tooltip), n => HudChrome.PlaceUnder(n, box), this);
            area.Exit += _ => _tip?.Hide(this);
            Layout();
        }

        private static string Css(bool hover) => Ocb.PanelCss + SpeedHudCss + (hover ? SpeedHudHoverCss : "") + InlineCss;

        private void Layout()
        {
            ScreenBox box = CornerLayout.SpeedButton;
            Ocb.Flow(_el, Css(false), null, box.Width, box.Height);
        }

        /// <summary>createSpeedButton(env, onSpeed, tip): CornerLayout.SpeedButton in layer.hud; a click calls onSpeed.</summary>
        // js: scenes/battle/hud/chrome.js:123
        public static HudSpeedButton Create(BattleViewEnv env, Action onSpeed, HudTip tip) => new HudSpeedButton(env, onSpeed, tip);

        /// <summary>show(text, chevrons) = BattleSpeedButton.Show: the number (BattleSpeed.Label) and the chevrons (1–3).</summary>
        // js: scenes/battle/hud/chrome.js:142
        public void Show(string text, int chevrons)
        {
            if (!_el) return;
            int n = Math.Max(1, Math.Min(3, chevrons != 0 ? chevrons : 2));
            Ui.Clear(_iconBox);
            Ocb.Icon(_iconBox, "speed" + n.ToString(CultureInfo.InvariantCulture), CornerLayout.SpeedIconSize, DsTokens.Ink);
            Label.SetText(_label, text ?? "");
            Layout();
        }

        /// <summary>setVisible(on): hidden mid-hover must not come back lit.</summary>
        // js: scenes/battle/hud/chrome.js:149
        public void SetVisible(bool on)
        {
            if (!on) _tip?.Hide(this);
            if (_el) UiNode.Of(_el).Visible = on;
        }

        // js: scenes/battle/hud/chrome.js:153
        public void Destroy()
        {
            _tip?.Hide(this);
            if (_el) Ui.Remove(_el);
        }
    }

    /// <summary>createJournalButton(env, onJournal, tip) → { setOff, destroy }.</summary>
    // js: scenes/battle/hud/chrome.js:158
    public sealed class HudJournalButton
    {
        private readonly BattleViewEnv _env;
        private readonly HudTip _tip;
        private readonly OcbButton _btn;
        private bool _off;

        // js: scenes/battle/hud/chrome.js:158
        private HudJournalButton(BattleViewEnv env, Action onJournal, HudTip tip)
        {
            _env = env;
            _tip = tip;
            ScreenBox box = CornerLayout.JournalButton;
            _btn = Ocb.JournalButton(env.Layer.Hud);
            // el.removeAttribute('title'): the DS's title is never drawn here (the tip below says it)
            _btn.Root.name = "ocb-panel ocb-btn-sq ocbg-battle-journalbtn ocbg-battle-hit";
            _btn.Place(box.X, box.Y);
            BattleHitTag.Mark(_btn.Root);
            _btn.Click += () => { if (onJournal != null && !_off) onJournal(); };
            _btn.Area.Enter += _ => { if (!_off) _tip?.Show(HudChrome.CornerTip(HudChrome.JournalTip), n => HudChrome.PlaceUnder(n, box), this); };
            _btn.Area.Exit += _ => _tip?.Hide(this);
        }

        /// <summary>createJournalButton(env, onJournal, tip): the DS JournalButton at CornerLayout.JournalButton in layer.hud.</summary>
        // js: scenes/battle/hud/chrome.js:158
        public static HudJournalButton Create(BattleViewEnv env, Action onJournal, HudTip tip) => new HudJournalButton(env, onJournal, tip);

        /// <summary>
        /// setOff(on): the battle is over — off with the end-turn plate, at its 40 % (EnemyTurnShadeLayout.EndTurnAlpha), taking
        /// no click of its own (a click there moves the result on, as anywhere on the screen).
        /// </summary>
        // js: scenes/battle/hud/chrome.js:179
        public void SetOff(bool on)
        {
            _off = on;
            if (_off) _tip?.Hide(this);
            if (!_btn.Alive) return;
            _btn.Node.Opacity = _off ? EnemyTurnShadeLayout.EndTurnAlpha : 1f;
            _btn.Node.PointerEvents = _off ? PointerEvents.None : PointerEvents.Inherit;
        }

        // js: scenes/battle/hud/chrome.js:186
        public void Destroy()
        {
            _tip?.Hide(this);
            _btn.Remove();
        }
    }

    /// <summary>createSettingsButton(env, onSettings, tip) → { el, setVisible, destroy }.</summary>
    // js: scenes/battle/hud/chrome.js:223
    public sealed class HudSettingsButton
    {
        /// <summary>.ocbg-battle-journalbtn.ocb-btn-sq (hud.css:151) on the DS .ocb-panel .ocb-btn-sq, placed absolutely.</summary>
        private static readonly string ElCss = Ocb.PanelCss + Ocb.BtnSqCss + "position: absolute; box-sizing: border-box;";

        private readonly BattleViewEnv _env;
        private readonly HudTip _tip;
        private readonly RectTransform _el;

        // js: scenes/battle/hud/chrome.js:223
        private HudSettingsButton(BattleViewEnv env, Action onSettings, HudTip tip)
        {
            _env = env;
            _tip = tip;
            ScreenBox box = HudChrome.SettingsBox;
            _el = Ocb.El(env.Layer.Hud, "ocb-panel ocb-btn-sq ocbg-battle-journalbtn ocbg-battle-settingsbtn ocbg-battle-hit", ElCss);
            // gearIcon(30): GEAR_PATH stroked in ink, 2.2 wide, round caps and joins, no fill (chrome.js:205)
            PathGraphic gear = PathGraphic.Icon(_el, ExtraIcons.Get("gear"), 30f, DsTokens.Ink, null, IconDef.DefaultStrokeWidth);
            Ocb.NoShrink(gear);
            UiNode.Of(_el).Place(box.X, box.Y);
            Ocb.Flow(_el, ElCss);
            ClickArea area = ClickArea.On(_el);
            BattleHitTag.Mark(_el);
            area.HoverChanged += on => { if (_el) Ocb.Restyle(_el, ElCss + (on ? Ocb.SpeedHoverCss : "")); };   // .ocb-btn-sq:hover
            area.Click += _ =>
            {
                _env.Audio.Play("uiClick");
                onSettings?.Invoke();
            };
            area.Enter += _ => _tip?.Show(HudChrome.CornerTip(HudChrome.SettingsTip), n => HudChrome.PlaceUnder(n, box), this);
            area.Exit += _ => _tip?.Hide(this);
        }

        /// <summary>createSettingsButton(env, onSettings, tip): the gear at SettingsBox in layer.hud; a click plays uiClick and calls onSettings.</summary>
        // js: scenes/battle/hud/chrome.js:223
        public static HudSettingsButton Create(BattleViewEnv env, Action onSettings, HudTip tip) => new HudSettingsButton(env, onSettings, tip);

        // js: scenes/battle/hud/chrome.js:236
        public RectTransform El => _el ? _el : null;

        // js: scenes/battle/hud/chrome.js:237
        public void SetVisible(bool on)
        {
            if (!on) _tip?.Hide(this);
            if (_el) UiNode.Of(_el).Visible = on;
        }

        // js: scenes/battle/hud/chrome.js:238
        public void Destroy()
        {
            _tip?.Hide(this);
            if (_el) Ui.Remove(_el);
        }
    }

    /// <summary>createResultCard(env) → { show, now, destroy }.</summary>
    // js: scenes/battle/hud/chrome.js:278
    public sealed class HudResultCard
    {
        // ---- RESULT_PAPER (chrome.js:273) ----
        private const float WonW = 720f;
        private const float LostW = 922f;
        private const float LostH = 626f;
        private const float Rise = 80f;
        private const float ItemStagger = 120f;
        private const float BarMs = 400f;
        private const float ItemMs = 200f;
        private const float ScaleFrom = 0.94f;

        // ---- hud.css:159-173 ----
        /// <summary>.ocbg-battle-result.ocb-end (hud.css:159).</summary>
        private const string ResultCss = "position: absolute; inset: 0; width: 1920px; height: 1080px; z-index: 3;";
        /// <summary>.ocbg-battle-result.is-lost.ocb-end (hud.css:160).</summary>
        private const string ResultLostCss = "background: color-mix(in srgb, var(--flood, #06090c) 40%, transparent);";
        /// <summary>.ocbg-battle-result-paper (hud.css:161).</summary>
        private const string PaperCss = "box-sizing: border-box; pointer-events: auto; transform-origin: 50% 50%;";
        /// <summary>.ocbg-battle-result-paper.is-lost (hud.css:162).</summary>
        private const string PaperLostCss = "display: flex; flex-direction: column; align-items: stretch; gap: 14px; padding: 48px 72px 40px; text-align: left;";
        /// <summary>.ocbg-battle-result-title (hud.css:163).</summary>
        private const string TitleCss = "margin: 0 0 6px; font: 800 48px/60px var(--font-display); letter-spacing: 0.08em; text-align: center;";
        /// <summary>.ocbg-battle-result-line (hud.css:165).</summary>
        private const string LineCss = "font: 400 22px/32px var(--font-body); text-align: center;";
        /// <summary>.ocbg-battle-result-bars (hud.css:166).</summary>
        private const string BarsCss = "display: flex; flex-direction: column; gap: 8px; margin: 6px 64px;";
        /// <summary>.ocbg-battle-result-bars-head (hud.css:167).</summary>
        private const string BarsHeadCss = "font: 400 18px/24px var(--font-body); opacity: 0.8;";
        /// <summary>.ocbg-battle-result-bar (hud.css:168).</summary>
        private const string BarCss = "display: grid; grid-template-columns: 96px 1fr 48px; align-items: center; column-gap: 14px; font: 400 20px/28px var(--font-body);";
        /// <summary>.ocbg-battle-result-bar b (hud.css:169).</summary>
        private const string BarBCss = "text-align: right; font: 600 22px/28px var(--font-mono);";
        /// <summary>.ocbg-battle-result-bar-track (hud.css:170).</summary>
        private const string TrackCss = "position: relative; height: 16px; border-radius: 3px; background: color-mix(in srgb, var(--washi-ink) 12%, transparent); overflow: hidden;";
        /// <summary>.ocbg-battle-result-bar-fill (hud.css:171) and its inline background.</summary>
        private const string FillCss = "position: absolute; left: 0; top: 0; bottom: 0; width: 0; border-radius: 3px;";
        /// <summary>.ocbg-battle-result-paper .ocbg-battle-result-button (hud.css:172) over the DS's .ocb-end-paper button.</summary>
        private const string ButtonCss = "align-self: center; margin-top: 18px;";
        /// <summary>.ocbg-battle-result-button:focus-visible (hud.css:173).</summary>
        private const string ButtonFocusCss = "outline: 3px solid var(--amber); outline-offset: 3px;";

        // the grid's columns (96px 1fr 48px, column-gap 14px) and its row (align-items: center)
        private const float BarLabelW = 96f;
        private const float BarValueW = 48f;
        private const float BarColGap = 14f;
        private const float BarRowH = 28f;
        private const float TrackH = 16f;
        private const float BarsGap = 8f;
        private const float BarsMarginY = 6f;
        private const float BarsMarginX = 64f;
        private const float TitleMarginBottom = 6f;
        private const float PaperGap = 14f;
        private const float ButtonMarginTop = 18f;

        private sealed class Fill
        {
            public UiNode Node;
            /// <summary>dataset.to: the bar's share in % of its track.</summary>
            public float To;
            public float TrackW;
        }

        /// <summary>build()'s { el, paper, items, won } (and the bar fills querySelectorAll finds).</summary>
        private sealed class Built
        {
            public RectTransform El;
            public UiNode Node;
            public UiNode Paper;
            public readonly List<UiNode> Items = new List<UiNode>();
            public readonly List<Fill> Fills = new List<Fill>();
            public bool Won;
        }

        private readonly BattleViewEnv _env;
        private RectTransform _el;
        private Action _onButton;
        /// <summary>
        /// The page's sequential focus for 「手記へ」: once the battle is over the scene leaves Tab to the page
        /// (BattleScene.js:347 returns false for it), so Tab focuses the button — the one focusable element of the battle —
        /// and :focus-visible rings it. The kit's Focus stands in for the browser's (KeyStack moves it on an unswallowed Tab;
        /// a pointer press clears it).
        /// </summary>
        private FocusScope _scope;

        private HudResultCard(BattleViewEnv env)
        {
            _env = env;
        }

        /// <summary>The button leaves the page (el.remove()): its focus goes with it.</summary>
        private void ReleaseFocus()
        {
            if (_scope != null) Focus.PopScope(_scope);
            _scope = null;
        }

        /// <summary>createResultCard(env).</summary>
        // js: scenes/battle/hud/chrome.js:278
        public static HudResultCard Create(BattleViewEnv env) => new HudResultCard(env);

        // js: scenes/battle/hud/chrome.js:282
        private Built Build(BattleOutcome outcome, ResultInfo info)
        {
            if (_el) Ui.Remove(_el);
            ReleaseFocus();
            bool won = outcome == BattleOutcome.Won;
            ResultInfo i = info ?? new ResultInfo();
            var b = new Built { Won = won };
            RectTransform over = _env.Layer.Over;
            if (won)
            {
                // the DS end paper (.ocb-end > .ocb-end-paper: h2 and the lines), 720 wide
                OcbEnd end = Ocb.End(over, DepictionText.OutcomeText(outcome), i.Lines ?? Array.Empty<string>(), null);
                end.Root.name = "ocb-end ocbg-battle-result is-won";
                end.Paper.Root.name = "ocb-end-paper ocbg-battle-result-paper is-won";
                // .ocb-end takes no pointer (the layer's pointer-events: none); the paper does (pointer-events: auto)
                if (end.Box) end.Box.raycastTarget = false;
                if (end.Paper.Box) end.Paper.Box.raycastTarget = true;
                b.El = end.Root;
                b.Paper = end.Paper.Node;
            }
            else
            {
                string elCss = Ocb.EndCss + ResultCss + ResultLostCss;
                RectTransform el = Ocb.El(over, "ocb-end ocbg-battle-result is-lost", elCss);
                UiNode.Of(el).Place(0f, 0f, L.W, L.H);
                string paperCss = Ocb.EndPaperCss + PaperCss + PaperLostCss + "width: " + Ocb.Num(LostW) + "px; min-height: " + Ocb.Num(LostH) + "px;";
                RectTransform paper = Ocb.El(el, "ocb-end-paper ocbg-battle-result-paper is-lost", paperCss);
                BoxGraphic pg = paper.GetComponent<BoxGraphic>();
                if (pg) pg.raycastTarget = true;
                BuildLost(b, paper, paperCss, outcome, i);
                b.El = el;
                b.Paper = UiNode.Of(paper);
            }
            _el = b.El;
            b.Node = UiNode.Of(b.El);
            b.Node.Z = 3;
            b.Node.Opacity = 0f;
            return b;
        }

        /// <summary>
        /// The lost paper's flex column (gap 14, items stretched, padding 48 72 40): the title, the lines, the bars (head and one
        /// grid row each), the stats and 「手記へ」, laid out from the top; min-height 626, centred on the screen.
        /// </summary>
        // js: scenes/battle/hud/chrome.js:292
        private void BuildLost(Built b, RectTransform paper, string paperCss, BattleOutcome outcome, ResultInfo i)
        {
            BoxStyle ps = Ocb.BoxOf(paperCss);
            Insets bw = ps.BorderWidths, pad = ps.Padding;
            float x0 = bw.Left + pad.Left, innerW = LostW - bw.Horizontal - pad.Horizontal;
            float y = bw.Top + pad.Top;
            bool any = false;
            float Start(float marginTop)
            {
                if (any) y += PaperGap;
                any = true;
                y += marginTop;
                return y;
            }
            string inherit = Ocb.EndPaperCss + PaperLostCss;

            // the title (h2)
            TextStyle ts = Ocb.TextOf(inherit + TitleCss);
            // i.title || OutcomeText: an empty title falls back too
            string title = string.IsNullOrEmpty(i.Title) ? DepictionText.OutcomeText(outcome) : i.Title;
            float th = Ocb.TextHeight(title, ts, innerW);
            TextMeshProUGUI t = Label.Make(paper, title, ts, x0, Start(0f), innerW, th, "ocbg-battle-result-title");
            b.Items.Add(UiNode.Of(t.rectTransform));
            y += th + TitleMarginBottom;

            // the lines
            TextStyle ls = Ocb.TextOf(inherit + Ocb.EndPaperPCss + LineCss);
            foreach (string line in i.Lines ?? Array.Empty<string>())
            {
                float h = Ocb.TextHeight(line ?? "", ls, innerW);
                TextMeshProUGUI p = Label.Make(paper, line ?? "", ls, x0, Start(0f), innerW, h, "ocbg-battle-result-line");
                b.Items.Add(UiNode.Of(p.rectTransform));
                y += h;
            }

            // the bars: the values at once, the fills growing
            if (i.Bars != null && i.Bars.Count > 0)
            {
                int most = 1;
                foreach (ResultBar bar in i.Bars) most = Math.Max(most, bar.Value);
                float boxW = innerW - 2f * BarsMarginX;
                RectTransform box = Ocb.El(paper, "ocbg-battle-result-bars", BarsCss);
                UiNode.Of(box).Place(x0 + BarsMarginX, Start(BarsMarginY), boxW, 0f);
                float by = 0f;
                bool anyRow = false;
                if (!string.IsNullOrEmpty(i.BarsLabel))
                {
                    TextStyle hs = Ocb.TextOf(inherit + BarsCss + BarsHeadCss);
                    float hh = Ocb.TextHeight(i.BarsLabel, hs, boxW);
                    TextMeshProUGUI head = Label.Make(box, i.BarsLabel, hs, 0f, by, boxW, hh, "ocbg-battle-result-bars-head");
                    UiNode.Of(head.rectTransform).Opacity = 0.8f;
                    by += hh;
                    anyRow = true;
                }
                float trackW = boxW - BarLabelW - BarValueW - 2f * BarColGap;
                TextStyle labelStyle = Ocb.TextOf(inherit + BarsCss + BarCss);
                TextStyle valueStyle = Ocb.TextOf(inherit + BarsCss + BarCss + BarBCss);
                foreach (ResultBar bar in i.Bars)
                {
                    if (anyRow) by += BarsGap;
                    anyRow = true;
                    RectTransform row = Ui.Div(box, "ocbg-battle-result-bar", 0f, by, boxW, BarRowH);
                    UiNode.Of(row);
                    Label.Make(row, bar.Label ?? "", labelStyle, 0f, (BarRowH - labelStyle.LineHeightPx) * 0.5f, BarLabelW, labelStyle.LineHeightPx, "span");
                    RectTransform track = Ocb.El(row, "ocbg-battle-result-bar-track", TrackCss);
                    UiNode.Of(track).Place(BarLabelW + BarColGap, (BarRowH - TrackH) * 0.5f, trackW, TrackH);
                    track.gameObject.AddComponent<RectMask2D>();   // overflow: hidden
                    RectTransform fill = Ocb.El(track, "ocbg-battle-result-bar-fill", FillCss + "background: " + (bar.Color ?? "transparent") + ";");
                    UiNode fn = UiNode.Of(fill);
                    fn.Place(0f, 0f, 0f, TrackH);
                    b.Fills.Add(new Fill { Node = fn, To = (float)bar.Value / most * 100f, TrackW = trackW });
                    Label.Make(row, bar.Value.ToString(CultureInfo.InvariantCulture), valueStyle,
                        boxW - BarValueW, (BarRowH - valueStyle.LineHeightPx) * 0.5f, BarValueW, valueStyle.LineHeightPx, "b");
                    by += BarRowH;
                }
                UiNode.Of(box).H = by;
                b.Items.Add(UiNode.Of(box));
                y += by + BarsMarginY;
            }

            // the stats
            foreach (string line in i.Stats ?? Array.Empty<string>())
            {
                float h = Ocb.TextHeight(line ?? "", ls, innerW);
                TextMeshProUGUI p = Label.Make(paper, line ?? "", ls, x0, Start(0f), innerW, h, "ocbg-battle-result-line");
                b.Items.Add(UiNode.Of(p.rectTransform));
                y += h;
            }

            // 手記へ
            if (!string.IsNullOrEmpty(i.Button))
            {
                string css = Ocb.EndPaperButtonCss + ButtonCss;
                RectTransform btn = Ocb.El(paper, "ocbg-battle-result-button ocbg-battle-hit", css);
                BoxStyle bs = Ocb.BoxOf(css);
                Insets bb = bs.BorderWidths, bp = bs.Padding;
                TextStyle bt = Ocb.TextOf(inherit + css);
                float tw = Ocb.TextWidth(i.Button, bt);
                float w = tw + bp.Horizontal + bb.Horizontal, h = bt.LineHeightPx + bp.Vertical + bb.Vertical;
                Label.Make(btn, i.Button, bt, bb.Left + bp.Left, bb.Top + bp.Top, null, null, "text");
                // align-self: center
                UiNode.Of(btn).Place(x0 + (innerW - w) * 0.5f, Start(ButtonMarginTop), w, h);
                ClickArea area = ClickArea.On(btn);
                BattleHitTag.Mark(btn);
                area.Click += _ => _onButton?.Invoke();
                // :focus-visible — Tab reaches it (see _scope); the press is btn.click()
                _scope = Focus.PushScope("battle-result");
                _scope.Add(btn, () => _onButton?.Invoke(), on => { if (btn) Ocb.Restyle(btn, css + (on ? ButtonFocusCss : "")); });
                b.Items.Add(UiNode.Of(btn));
                y += h;
            }

            float paperH = Math.Max(LostH, y + pad.Bottom + bw.Bottom);
            // .ocb-end centres the paper
            UiNode.Of(paper).Place((L.W - LostW) * 0.5f, (L.H - paperH) * 0.5f, LostW, paperH);
        }

        // js: scenes/battle/hud/chrome.js:325
        private static void SettleItems(Built b)
        {
            foreach (UiNode x in b.Items) if (x) x.Opacity = 1f;
            foreach (Fill f in b.Fills) if (f.Node) f.Node.W = f.TrackW * f.To / 100f;
        }

        /// <summary>
        /// show(outcome, ms, info, { onButton }): the DS end paper in layer.over — a win's 720-wide paper scaling 0.94 → 1, or a
        /// loss's result screen rising 80 px, its items 120 apart from ms/2, the bars over 400. `onButton` its button's click.
        /// </summary>
        // js: scenes/battle/hud/chrome.js:331
        public Promise Show(BattleOutcome outcome, float ms, ResultInfo info, Action onButton)
        {
            _onButton = onButton;
            Built b = Build(outcome, info);
            BattleClock clock = _env.Clock;
            if (!(ms > 0f))
            {
                b.Node.Opacity = 1f;
                SettleItems(b);
                return Promise.Resolved();
            }
            foreach (UiNode x in b.Items) x.Opacity = 0f;
            foreach (Fill f in b.Fills) f.Node.W = 0f;
            Promise enter = clock.Run(ms, Ease.Out, t =>
            {
                if (!b.Node || !b.Paper) return;
                b.Node.Opacity = HudChrome.Fixed(Math.Min(1f, t * 2f), 3);
                if (b.Won) b.Paper.Scale = HudChrome.Fixed(ScaleFrom + (1f - ScaleFrom) * t, 4);
                else b.Paper.TranslateY = HudChrome.Fixed(Rise * (1f - t), 1);
            }).Then(() =>
            {
                if (b.Paper) b.Paper.ResetTransform();
                if (b.Node) b.Node.Opacity = 1f;
            });
            if (b.Won) return enter.Then(() => SettleItems(b));
            // §5.19 順 6: the items from the top, 120 apart; the bars grow over 400 (their values stand at once).
            var all = new List<Promise> { enter };
            for (int k = 0; k < b.Items.Count; k++)
            {
                UiNode x = b.Items[k];
                all.Add(clock.Wait(ms * 0.5f + k * ItemStagger).Then(() => clock.Run(ItemMs, Ease.Out, t =>
                {
                    if (x) x.Opacity = HudChrome.Fixed(t, 3);
                })));
            }
            Promise bars = clock.Wait(ms * 0.5f + ItemStagger * 2f).Then(() => clock.Run(BarMs, Ease.Out, t =>
            {
                foreach (Fill f in b.Fills) if (f.Node) f.Node.W = f.TrackW * HudChrome.Fixed(f.To * t, 2) / 100f;
            }));
            all.Insert(1, bars);
            return Promise.All(all).Then(() => SettleItems(b));
        }

        /// <summary>now(outcome, info, { onButton }): the paper at once.</summary>
        // js: scenes/battle/hud/chrome.js:352
        public void Now(BattleOutcome outcome, ResultInfo info, Action onButton)
        {
            _onButton = onButton;
            Built b = Build(outcome, info);
            b.Node.Opacity = 1f;
            SettleItems(b);
        }

        // js: scenes/battle/hud/chrome.js:358
        public void Destroy()
        {
            if (_el) Ui.Remove(_el);
            ReleaseFocus();
            _el = null;
            _onButton = null;
        }
    }

    /// <summary>createChainBanner(env) → { play, destroy }.</summary>
    // js: scenes/battle/hud/chrome.js:368
    public sealed class HudChainBanner
    {
        // ---- CHAIN_BANNER (chrome.js:367) ----
        private const float InMs = 300f;
        private const float HoldMs = 700f;
        private const float OutMs = 400f;

        /// <summary>hud.css:177 .ocbg-battle-chainbanner.</summary>
        private const string BoardCss =
            "position: absolute; inset: 0; width: 1920px; height: 1080px; display: flex; align-items: center; justify-content: center; "
            + "background: color-mix(in srgb, var(--ground) 86%, transparent); pointer-events: none; z-index: 2;";
        /// <summary>hud.css:178 .ocbg-battle-chainbanner-band.</summary>
        private const string BandCss =
            "display: flex; flex-direction: column; align-items: center; gap: 12px; padding: 28px 96px; "
            + "border-top: 2px solid color-mix(in srgb, var(--ink) 40%, transparent); border-bottom: 2px solid color-mix(in srgb, var(--ink) 40%, transparent);";
        /// <summary>hud.css:179 .ocbg-battle-chainbanner-band b.</summary>
        private const string BandBCss = "font: 800 64px/72px var(--font-display); letter-spacing: 0.12em; color: var(--ink);";
        /// <summary>hud.css:180 .ocbg-battle-chainbanner-band span.</summary>
        private const string BandSpanCss = "font: 700 28px/36px var(--font-display); color: var(--ink); letter-spacing: 0.06em;";
        private const float BandGap = 12f;

        private readonly BattleViewEnv _env;
        private RectTransform _el;

        private HudChainBanner(BattleViewEnv env)
        {
            _env = env;
        }

        /// <summary>createChainBanner(env).</summary>
        // js: scenes/battle/hud/chrome.js:368
        public static HudChainBanner Create(BattleViewEnv env) => new HudChainBanner(env);

        /// <summary>
        /// play(title, name, scale, onOut): the dark board, 「n 戦目」 and the name come in (300 out), stand (700), and the board
        /// goes (400 in) — onOut(400) as it starts going. scale 0 (演出短縮 / effects off) shows nothing and calls onOut at once.
        /// </summary>
        // js: scenes/battle/hud/chrome.js:373
        public Promise Play(string title, string name, float scale = 1f, Action<float> onOut = null)
        {
            if (_el) Ui.Remove(_el);
            if (!(scale > 0f))
            {
                onOut?.Invoke(0f);
                return Promise.Resolved();
            }
            BattleClock clock = _env.Clock;
            RectTransform el = Ocb.El(_env.Layer.Over, "ocbg-battle-chainbanner", BoardCss);
            UiNode board = UiNode.Of(el);
            board.Place(0f, 0f, L.W, L.H);
            board.Z = 2;
            board.PointerEvents = PointerEvents.None;
            RectTransform band = Ocb.El(el, "ocbg-battle-chainbanner-band", BandCss);
            BoxStyle bs = Ocb.BoxOf(BandCss);
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            TextStyle tb = Ocb.TextOf(BandCss + BandBCss);
            TextStyle ts = Ocb.TextOf(BandCss + BandSpanCss);
            float titleW = Ocb.TextWidth(title ?? "", tb);
            bool hasName = !string.IsNullOrEmpty(name);
            float nameW = hasName ? Ocb.TextWidth(name, ts) : 0f;
            float innerW = Math.Max(titleW, nameW);
            float x0 = bw.Left + pad.Left, y = bw.Top + pad.Top;
            Label.Make(band, title ?? "", tb, x0 + (innerW - titleW) * 0.5f, y, null, null, "b");
            y += tb.LineHeightPx;
            if (hasName)
            {
                y += BandGap;
                Label.Make(band, name, ts, x0 + (innerW - nameW) * 0.5f, y, null, null, "span");
                y += ts.LineHeightPx;
            }
            float bandW = innerW + pad.Horizontal + bw.Horizontal, bandH = y + pad.Bottom + bw.Bottom;
            UiNode panel = UiNode.Of(band);
            panel.Place((L.W - bandW) * 0.5f, (L.H - bandH) * 0.5f, bandW, bandH);
            _el = el;
            board.Opacity = 1f;
            panel.Opacity = 0f;
            return clock.Run(InMs, Ease.Out, t =>
                {
                    if (!panel) return;
                    panel.Opacity = HudChrome.Fixed(t, 3);
                    panel.PropTranslateX = 0f;
                    panel.PropTranslateY = HudChrome.Fixed(12f * (1f - t), 1);
                })
                .Then(() => clock.Wait(HoldMs))
                .Then(() =>
                {
                    onOut?.Invoke(OutMs);
                    return clock.Run(OutMs, Ease.In, t =>
                    {
                        if (_el) UiNode.Of(_el).Opacity = HudChrome.Fixed(1f - t, 3);
                    });
                })
                .Then(() =>
                {
                    if (_el) Ui.Remove(_el);
                    _el = null;
                });
        }

        // js: scenes/battle/hud/chrome.js:387
        public void Destroy()
        {
            if (_el) Ui.Remove(_el);
            _el = null;
        }
    }

    /// <summary>createLossInk(env) → { show, fadeWord, destroy }.</summary>
    // js: scenes/battle/hud/chrome.js:396
    public sealed class HudLossInk
    {
        /// <summary>hud.css:185 .ocbg-battle-ink.</summary>
        private const string InkCss = "position: absolute; left: 0; top: 0; width: 1920px; height: 1080px; pointer-events: none; overflow: hidden;";
        /// <summary>hud.css:186 .ocbg-battle-ink-fill.</summary>
        private const string FillCss =
            "position: absolute; left: 0; width: 1920px; height: 1240px; "
            + "background: linear-gradient(180deg, transparent 0, color-mix(in srgb, var(--flood, #06090c) 94%, transparent) 160px);";
        /// <summary>hud.css:187 .ocbg-battle-ink-word.</summary>
        private const string WordCss =
            "position: absolute; left: 0; width: 1920px; top: 560px; text-align: center; font: 700 64px/80px var(--font-display); "
            + "color: var(--ink); white-space: nowrap;";
        /// <summary>The fill's travel: from top 1080 to top −160 (its 1240 px reach the screen's bottom).</summary>
        private const float Travel = 1240f;
        private const float FillH = 1240f;
        private const float WordTop = 560f;

        private readonly BattleViewEnv _env;
        private RectTransform _el;
        private TextMeshProUGUI _word;
        private TextStyle _wordStyle;

        private HudLossInk(BattleViewEnv env)
        {
            _env = env;
        }

        /// <summary>createLossInk(env).</summary>
        // js: scenes/battle/hud/chrome.js:396
        public static HudLossInk Create(BattleViewEnv env) => new HudLossInk(env);

        /// <summary>show(word, inkMs, wordMs): the ink (flood) rises from the bottom over inkMs (in), then `word` comes in over wordMs (out), its letters drawing apart.</summary>
        // js: scenes/battle/hud/chrome.js:400
        public Promise Show(string word, float inkMs, float wordMs)
        {
            if (_el) Ui.Remove(_el);
            BattleClock clock = _env.Clock;
            RectTransform el = Ui.Div(_env.Layer.Over, "ocbg-battle-ink", 0f, 0f, L.W, L.H);
            UiNode.Of(el).PointerEvents = PointerEvents.None;
            el.gameObject.AddComponent<RectMask2D>();   // overflow: hidden
            BoxGraphic fill = BoxGraphic.Make(el, FillCss, 0f, L.H, L.W, FillH, "ocbg-battle-ink-fill");
            UiNode fillNode = UiNode.Of(fill.rectTransform);
            _wordStyle = Ocb.TextOf(WordCss);
            TextMeshProUGUI text = Label.Make(el, word ?? "", _wordStyle, 0f, WordTop, L.W, _wordStyle.LineHeightPx, "ocbg-battle-ink-word");
            UiNode textNode = UiNode.Of(text.rectTransform);
            // layer.over.appendChild(el): z-index auto — under a result card (z 3) or chain banner (z 2) already standing
            UiNode.ResortSiblings(_env.Layer.Over);
            _el = el;
            _word = text;
            TextStyle style = _wordStyle;
            void Place(float t)
            {
                if (fillNode) fillNode.Y = HudChrome.Fixed(L.H - Travel * t, 1);
            }
            void Spell(float t)
            {
                if (!text) return;
                textNode.Opacity = HudChrome.Fixed(t, 3);
                Label.Apply(text, style.WithSpacing(HudChrome.Fixed(0.06f + 0.34f * t, 3)));
            }
            Place(0f);
            Spell(0f);
            Promise rise;
            if (inkMs > 0f) rise = clock.Run(inkMs, Ease.In, (Action<float>)Place);
            else
            {
                Place(1f);
                rise = Promise.Resolved();
            }
            return rise.Then(() =>
            {
                if (wordMs > 0f) return clock.Run(wordMs, Ease.Out, (Action<float>)Spell);
                Spell(1f);
                return Promise.Resolved();
            });
        }

        /// <summary>fadeWord(ms): 「力尽きた」 gives way as the result screen comes up (in).</summary>
        // js: scenes/battle/hud/chrome.js:418
        public Promise FadeWord(float ms)
        {
            TextMeshProUGUI text = _el ? _word : null;
            if (!text) return Promise.Resolved();
            UiNode n = UiNode.Of(text.rectTransform);
            if (!(ms > 0f))
            {
                n.Opacity = 0f;
                return Promise.Resolved();
            }
            // parseFloat(text.style.opacity) || 1
            float from = n.Opacity > 0f ? n.Opacity : 1f;
            return _env.Clock.Run(ms, Ease.In, t =>
            {
                if (n) n.Opacity = HudChrome.Fixed(from * (1f - t), 3);
            });
        }

        // js: scenes/battle/hud/chrome.js:425
        public void Destroy()
        {
            if (_el) Ui.Remove(_el);
            _el = null;
            _word = null;
        }
    }
}
