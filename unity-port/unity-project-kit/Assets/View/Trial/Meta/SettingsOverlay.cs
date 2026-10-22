// Port of scenes/meta/settings.js (設定・セーブ, CAMP_FACILITIES_DESIGN.md §3.5, dungeon_exploration_v4.md §6.2): the
// battle speed (×1.0 / ×1.25 / ×1.5, BattleSpeed's labels and chevrons), 演出短縮, the volumes and mute (the game's audio,
// which persists itself), 「保存して閉じる」 and 「閉じる」. An overlay over whatever screen opened it; Esc closes it. The
// speed is one value shared with the battle screen's switch.
//
// The look is meta.css's .ocbg-meta-settings rules (meta.css:295-320) copied verbatim below. The panel is a flex column
// (padding 28 36 24, gap 6) laid out here in stage px as the browser lays it out, then centred: top =
// max(24, round((1080 − offsetHeight) / 2)). Every word comes from MetaText (Trial.Script), the speed's labels and
// chevrons from BattleSpeed, the stored values from TrialSettings (Trial.Bridge).
using System;
using System.Collections.Generic;
using Depiction;
using TMPro;
using Trial.Audio;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using UnityEngine.EventSystems;
using TextMeasure = Trial.Kit.TextMeasure;

namespace Trial.Meta
{
    public sealed class SettingsOverlay : IOverlay
    {
        // ---- meta.css: settings, verbatim -----------------------------------------------------------------------------

        /// <summary>meta.css:296 .ocbg-meta-settings .ocbg-meta-settings-panel.</summary>
        public const string PanelCss = "padding: 28px 36px 24px; display: flex; flex-direction: column; gap: 6px;";

        /// <summary>meta.css:297 .ocbg-meta-settings header.</summary>
        public const string HeaderCss = "display: flex; align-items: center; justify-content: space-between; margin-bottom: var(--space-12);";

        /// <summary>meta.css:298 .ocbg-meta-settings header h2.</summary>
        public const string H2Css = "font: 700 34px/1.2 var(--font-display); letter-spacing: .16em;";

        /// <summary>meta.css:299 .ocbg-meta-settings .ocbg-meta-set-row.</summary>
        public const string RowCss = "position: relative; display: grid; grid-template-columns: 176px 1fr auto; align-items: center; gap: var(--space-16); min-height: 64px; padding: 6px 14px; border-radius: var(--radius-panel); transition: background .14s;";

        /// <summary>meta.css:300 .ocbg-meta-settings .ocbg-meta-set-row.is-focus.</summary>
        public const string RowFocusCss = "background: color-mix(in srgb, var(--ink) 6%, transparent);";

        /// <summary>meta.css:301 .ocbg-meta-settings .ocbg-meta-set-row.is-focus::before.</summary>
        public const string RowFocusBeforeCss = "content: \"\"; position: absolute; left: 0; top: 14px; bottom: 14px; width: 3px; border-radius: 2px; background: var(--amber);";

        /// <summary>meta.css:302 .ocbg-meta-settings .ocbg-meta-set-label.</summary>
        public const string LabelCss = "font: 700 22px/1.2 var(--font-display); letter-spacing: .08em;";

        /// <summary>meta.css:303 .ocbg-meta-settings .ocbg-meta-seg.</summary>
        public const string SegCss = "display: flex; gap: var(--space-8);";

        /// <summary>meta.css:304 .ocbg-meta-settings .ocbg-meta-seg button.</summary>
        public const string SegButtonCss = "min-width: 132px; height: 48px; padding: 0 16px; display: inline-flex; align-items: center; justify-content: center; gap: 8px; background: var(--panel); border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-panel); font: 700 20px/1 var(--font-body); transition: border-color .14s, background .14s;";

        /// <summary>meta.css:305 .ocbg-meta-settings .ocbg-meta-seg button span.</summary>
        public const string SegButtonSpanCss = "font-family: var(--font-mono); font-weight: 600;";

        /// <summary>meta.css:306 .ocbg-meta-settings .ocbg-meta-seg button:hover.</summary>
        public const string SegButtonHoverCss = "border-color: var(--ink);";

        /// <summary>meta.css:307 .ocbg-meta-settings .ocbg-meta-seg button.is-on.</summary>
        public const string SegButtonOnCss = "border: var(--stroke-frame) solid var(--amber); background: color-mix(in srgb, var(--amber) 16%, var(--panel));";

        /// <summary>meta.css:308 .ocbg-meta-settings .ocbg-meta-set-note.</summary>
        public const string NoteCss = "font: 400 16px/1.45 var(--font-body); padding-left: 206px; margin-top: -4px; margin-bottom: 4px;";

        /// <summary>meta.css:309 .ocbg-meta-settings .ocbg-meta-set-h3.</summary>
        public const string H3Css = "font: 700 20px/1 var(--font-body); letter-spacing: .1em; margin-top: 12px; padding: 16px 14px 6px; border-top: var(--stroke-rule) solid var(--rule);";

        /// <summary>meta.css:310 .ocbg-meta-settings .ocbg-meta-slider.</summary>
        public const string SliderCss = "position: relative; height: 10px; border-radius: 5px; background: color-mix(in srgb, var(--ground) 80%, transparent); border: var(--stroke-rule) solid var(--rule); cursor: pointer; touch-action: none;";

        /// <summary>meta.css:311 .ocbg-meta-settings .ocbg-meta-slider::after (the 14 px taller hit area).</summary>
        public const string SliderAfterCss = "content: \"\"; position: absolute; left: 0; right: 0; top: -14px; bottom: -14px;";

        /// <summary>meta.css:312 .ocbg-meta-settings .ocbg-meta-slider-fill.</summary>
        public const string SliderFillCss = "position: absolute; left: 0; top: 0; bottom: 0; border-radius: 5px; background: var(--amber);";

        /// <summary>meta.css:313 .ocbg-meta-settings .ocbg-meta-slider-knob.</summary>
        public const string SliderKnobCss = "position: absolute; top: 50%; width: 24px; height: 24px; margin: -12px 0 0 -12px; border-radius: 50%; background: var(--panel-solid); border: var(--stroke-frame) solid var(--amber); box-shadow: 0 0 10px color-mix(in srgb, var(--amber) 40%, transparent);";

        /// <summary>meta.css:314 .ocbg-meta-settings .ocbg-meta-slider-num.</summary>
        public const string SliderNumCss = "width: 56px; text-align: right; font: 600 24px/1 var(--font-mono);";

        /// <summary>meta.css:315 .ocbg-meta-settings footer.</summary>
        public const string FooterCss = "display: flex; align-items: center; justify-content: flex-end; gap: var(--space-16); margin-top: var(--space-16); padding-top: var(--space-16); border-top: var(--stroke-rule) solid var(--rule);";

        /// <summary>meta.css:316 .ocbg-meta-settings footer .ocbg-meta-set-note.</summary>
        public const string FooterNoteCss = "padding: 0; margin: 0 auto 0 0; max-width: 300px; flex: 1 1 auto; min-width: 0;";

        /// <summary>meta.css:318 .ocbg-meta-settings footer .ocbg-meta-btn, .ocbg-meta-settings footer .ocbg-meta-primary.</summary>
        public const string FooterButtonCss = "height: 56px; flex: none; white-space: nowrap;";

        /// <summary>meta.css:319 .ocbg-meta-settings footer .is-focus.</summary>
        public const string FooterFocusCss = "box-shadow: 0 0 0 2px var(--wick);";

        /// <summary>meta.css:320 .ocbg-meta-settings .ocbg-meta-keys.is-in-panel.</summary>
        public const string KeysInPanelCss = "margin-top: var(--space-12); text-align: right;";

        // ---- the panel's box in stage px (settings.js:40, meta.css:296-320) ----------------------------------------------

        private const float PanelX = 540f, PanelW = 840f;
        private const float Border = 1f, PadTop = 28f, PadX = 36f, PadBottom = 24f, Gap = 6f;
        private const float X0 = Border + PadX;                              // the content box's left
        private const float ContentW = PanelW - 2f * Border - 2f * PadX;     // 766
        private const float HeaderH = 44f;                                   // the × (44) is the tallest item
        private const float RowMinH = 64f, RowPadX = 14f, RowPadY = 6f, LabelCol = 176f, ColGap = 16f;
        private const float ControlX = RowPadX + LabelCol + ColGap;          // the second grid column (206)
        private const float SegH = 48f, SegMinW = 132f, SegPadX = 16f, SegGap = 8f, SegIcon = 22f;
        private const float TrackH = 10f, NumW = 56f, KnobSize = 24f, AfterPad = 14f;
        private const float FooterButtonH = 56f, FooterGap = 16f, FooterNoteMaxW = 300f;

        // ---- state ----------------------------------------------------------------------------------------------------

        /// <summary>The volumes and mute as the panel holds them (settings.js `vol`).</summary>
        private sealed class Vol
        {
            public double Master, Effects, Ambience;
            public double? Reverb;
            public bool Muted;
        }

        /// <summary>One focusable row (settings.js `rows`): its keys, its repaint and its .is-focus.</summary>
        private sealed class Row
        {
            public Action Left, Right, Enter, Paint;
            public Action<bool> Focus;
        }

        /// <summary>One option of a segmented row.</summary>
        private readonly struct SegOption<T>
        {
            public SegOption(T value, string label, string icon = null)
            {
                Value = value;
                Label = label;
                Icon = icon;
            }

            public T Value { get; }
            public string Label { get; }
            public string Icon { get; }
        }

        private readonly SettingsCtx _ctx;
        private readonly MetaSound _sound;
        private readonly ITrialAudio _audio;
        private readonly RectTransform _root;
        private readonly SceneClock _clock;
        private bool _alive = true;
        private bool _closing;

        private readonly TrialSettings _game;
        private BattleSpeedStep _speed;
        private bool _shortFx;
        private readonly Vol _vol;

        private readonly List<Row> _rows = new List<Row>();
        private int _focus;
        private readonly RectTransform _panel;
        private readonly UiNode _panelNode, _shadeNode;
        private readonly global::Trial.Kit.Styler _ps, _pd;
        private float _y;
        private bool _first = true;
        private Action _popKeys;
        private Action _unsub;

        // js: scenes/meta/settings.js:24
        private SettingsOverlay(RectTransform host, SettingsCtx ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _sound = MetaUtil.SoundOf(ctx);
            _audio = _sound.Audio;
            _root = MetaUtil.SceneRoot(host, "settings");
            _clock = MetaUtil.SceneClock(null, ctx.Stage);

            _game = ctx.Settings != null ? ctx.Settings.Clone() : new TrialSettings();   // { ...(ctx.settings || {}) }
            _speed = StepOf(_game.Speed);
            _shortFx = _game.ShortFx;
            // the audio's own settings; without an audio, the game's (or the defaults when there is none)
            _vol = _audio != null
                ? new Vol { Master = _audio.Master, Effects = _audio.Effects, Ambience = _audio.AmbienceVolume, Reverb = _audio.Reverb, Muted = _audio.Muted }
                : ctx.Settings != null
                    ? new Vol { Master = ctx.Settings.Master, Effects = ctx.Settings.Effects, Ambience = ctx.Settings.Ambience, Muted = ctx.Settings.Muted }
                    : new Vol { Master = 0.8, Effects = 1, Ambience = 0.6, Muted = false };

            BoxGraphic shade = MetaStyle.Shade(_root, onClick: () => Close());
            _shadeNode = UiNode.Of(shade.rectTransform);
            BoxGraphic panel = MetaStyle.Panel(_root, PanelX, 150f, PanelW, 1f);
            panel.name = "ocbg-meta-panel ocbg-meta-settings-panel";
            panel.raycastTarget = true;   // a click on the panel stays there (the shade under it closes)
            _panel = panel.rectTransform;
            _panelNode = UiNode.Of(_panel);
            _y = Border + PadTop;

            // header: 設定・セーブ and ×
            float hy = Next(HeaderH, 0f, 12f);
            TextStyle h2 = MetaStyle.Text(H2Css);
            Label.Make(_panel, MetaText.SettingsTitle, h2, X0, hy + (HeaderH - h2.LineHeightPx) / 2f, null, null, "h2");
            MetaButton x = MetaButton.X(_panel, X0 + ContentW - 44f, hy);
            x.Click += () => Close();

            var speeds = new List<SegOption<BattleSpeedStep>>();
            foreach (BattleSpeedStep s in BattleSpeed.Steps) speeds.Add(new SegOption<BattleSpeedStep>(s, BattleSpeed.Label(s), "speed" + BattleSpeed.Chevrons(s)));
            SegRow(MetaText.Speed, speeds, () => _speed, v => { _speed = v; Emit(); });
            SegRow(MetaText.ShortFx, new[] { new SegOption<bool>(true, MetaText.ShortFxOn), new SegOption<bool>(false, MetaText.ShortFxOff) },
                () => _shortFx, v => { _shortFx = v; Emit(); }, MetaText.ShortFxNote);

            // ---- volumes ----
            float h3y = Next(1f + 16f + 20f + 6f, 12f, 0f);
            BoxGraphic h3 = BoxGraphic.Make(_panel, MetaStyle.Without(H3Css, "font", "letter-spacing", "margin-top"), X0, h3y, ContentW, 1f + 16f + 20f + 6f, "ocbg-meta-set-h3");
            Label.Make(h3.rectTransform, MetaText.Volume, MetaStyle.Text(H3Css), 14f, 1f + 16f, null, null, "text");
            Slider(MetaText.Master, () => _vol.Master, v => _vol.Master = v, v => _audio?.SetMaster(v));
            Slider(MetaText.Effects, () => _vol.Effects, v => _vol.Effects = v, v => _audio?.SetEffects(v));
            Slider(MetaText.Ambience, () => _vol.Ambience, v => _vol.Ambience = v, v => _audio?.SetAmbienceVolume(v));
            SegRow(MetaText.Mute, new[] { new SegOption<bool>(true, MetaText.MuteOn), new SegOption<bool>(false, MetaText.MuteOff) },
                () => _vol.Muted, v => { _vol.Muted = v; _audio?.SetMuted(v); });

            // ---- save and close ----
            Footer();
            float ky = Next(MetaStyle.Text(MetaStyle.KeysCss).LineHeightPx, 12f, 0f);
            MetaStyle.Keys(_panel, MetaText.SettingsKeys, X0, ky, ContentW, right: true);

            // the panel's height is its content's; it is centred on the stage (never above 24)
            float height = _y + PadBottom + Border;
            float top = Math.Max(24, MetaUtil.JsRound((MetaUtil.H - MetaUtil.JsRound(height)) / 2.0));
            _panelNode.Place(PanelX, top, PanelW, height);

            PaintFocus();

            // keep the panel in step with a change made elsewhere (the battle's switch, a mute key)
            _unsub = _audio != null ? WatchAudio() : null;

            // ---- open ----
            _ps = global::Trial.Kit.Styler.Of(_panelNode, o: 0f, s: 0.97f, y: 10f);
            _pd = global::Trial.Kit.Styler.Of(_shadeNode, o: 0f);
            _clock.Tween(_pd, new[] { ("o", 1f) }, 200f, Ease.Out);
            _clock.Tween(_ps, new[] { ("o", 1f), ("s", 1f), ("y", 0f) }, 220f, Ease.Out);
            _sound.Play("uiClick");

            _popKeys = KeyStack.Push(OnKey);
        }

        // js: scenes/meta/settings.js:10
        /// <summary>stepOf(speed): a step's multiplier within 1e-6, else BattleSpeed.Default (TrialSettings.StepOf).</summary>
        private static BattleSpeedStep StepOf(double speed) => TrialSettings.StepOf(speed);

        /// <summary>mountSettings(host, ctx) → { destroy(), close() }.</summary>
        public static IOverlay Mount(RectTransform host, SettingsCtx ctx) => new SettingsOverlay(host, ctx);

        /// <summary>The panel's flex column: the next item's top (its margin-top added), the cursor moved past it and its margin-bottom.</summary>
        private float Next(float height, float marginTop = 0f, float marginBottom = 0f)
        {
            if (!_first) _y += Gap;
            _first = false;
            float top = _y + marginTop;
            _y = top + height + marginBottom;
            return top;
        }

        // js: scenes/meta/settings.js:49
        /// <summary>
        /// Every change hands on the whole panel as it stands now: the volumes and mute as they are at this moment (vol
        /// follows the sliders and the audio's changes), not as they were when the panel opened. The router writes the
        /// object back into the audio, so a stale copy would undo a volume change.
        /// </summary>
        private void Emit()
        {
            TrialSettings live = _ctx.Settings ?? _game;
            TrialSettings next = live.Clone();
            next.Master = _vol.Master;
            next.Effects = _vol.Effects;
            next.Ambience = _vol.Ambience;
            next.Muted = _vol.Muted;
            next.Speed = TrialSettings.SpeedOf(_speed);   // speed: BattleSpeed.Multiplier(speed), speedStep: speed
            next.ShortFx = _shortFx;
            if (_vol.Reverb.HasValue) next.Reverb = _vol.Reverb.Value;
            _ctx.OnChange?.Invoke(next);
        }

        /// <summary>A .ocbg-meta-set-row at its place in the column: the label, the focus states (.is-focus and its ::before).</summary>
        private RectTransform MakeRow(string label, out Action<bool> focus)
        {
            float top = Next(RowMinH);
            RectTransform row = Ui.Div(_panel, "ocbg-meta-set-row", X0, top, ContentW, RowMinH);
            UiNode.Of(row);
            BoxGraphic.On(row);
            CssStates states = CssStates.Of(row);
            states.Base(MetaStyle.Without(RowCss, "display", "position", "min-height")).State(".is-focus", RowFocusCss);
            states.Apply(instant: true);
            BoxGraphic before = BoxGraphic.Make(row, MetaStyle.Without(RowFocusBeforeCss, "content", "position", "left", "top", "bottom", "width"),
                0f, 14f, 3f, RowMinH - 28f, "::before");
            before.gameObject.SetActive(false);
            TextStyle ls = MetaStyle.Text(LabelCss);
            Label.Make(row, label, ls, RowPadX, (RowMinH - ls.LineHeightPx) / 2f, LabelCol, ls.LineHeightPx, "ocbg-meta-set-label");
            focus = on =>
            {
                if (states) states.SetState(".is-focus", on);
                if (before) before.gameObject.SetActive(on);
            };
            return row;
        }

        // js: scenes/meta/settings.js:58
        /// <summary>A segmented row: one button per option (the one in force .is-on); ←→ step through them.</summary>
        private void SegRow<T>(string label, IReadOnlyList<SegOption<T>> options, Func<T> get, Action<T> set, string note = null)
        {
            EqualityComparer<T> eq = EqualityComparer<T>.Default;
            RectTransform row = MakeRow(label, out Action<bool> focus);
            RectTransform seg = Ui.Div(row, "ocbg-meta-seg", ControlX, RowPadY + (RowMinH - 2f * RowPadY - SegH) / 2f, 0f, SegH);
            UiNode.Of(seg);
            var buttons = new List<(T Value, CssStates States)>();
            Action paint = null;
            float x = 0f;
            foreach (SegOption<T> o in options)
            {
                SegOption<T> option = o;
                RectTransform b = SegButton(seg, option.Label, option.Icon, x, out CssStates states, out ClickArea area);
                area.Click += _ =>
                {
                    if (!eq.Equals(get(), option.Value))
                    {
                        set(option.Value);
                        _sound.Play("uiClick");
                        paint();
                    }
                };
                buttons.Add((option.Value, states));
                x += UiNode.Of(b).W + SegGap;
            }
            UiNode.Of(seg).W = Math.Max(0f, x - SegGap);
            void Paint(bool instant)
            {
                foreach ((T value, CssStates states) in buttons)
                    if (states && states.SetState(".is-on", eq.Equals(value, get()), false)) states.Apply(instant);
            }
            paint = () => Paint(false);
            // the first paint runs before the row is in the document (settings.js:69-70): no transition
            Paint(true);
            if (note != null)
            {
                TextStyle ns = MetaStyle.Text(NoteCss);
                float nw = ContentW - 206f;
                float nh = Math.Max(ns.LineHeightPx, TextMeasure.Height(note, ns, nw));
                float ny = Next(nh, -4f, 4f);
                Label.Make(_panel, note, ns, X0 + 206f, ny, nw, nh, "ocbg-meta-set-note");
            }
            void Step(int by)
            {
                int i = -1;
                for (int k = 0; k < options.Count; k++) if (eq.Equals(options[k].Value, get())) { i = k; break; }
                int j = Math.Max(0, Math.Min(options.Count - 1, i + by));
                if (j != i)
                {
                    set(options[j].Value);
                    _sound.Play("uiClick");
                    paint();
                }
            }
            _rows.Add(new Row { Left = () => Step(-1), Right = () => Step(1), Paint = paint, Focus = focus });
        }

        /// <summary>
        /// .ocbg-meta-seg button at (x, 0) of seg: the icon (22 px ink) and the span (Plex Mono 600), centred; at least
        /// 132 wide, 48 tall; :hover and .is-on as the rules say (border-color and background over 140 ms).
        /// </summary>
        private static RectTransform SegButton(RectTransform seg, string text, string icon, float x, out CssStates states, out ClickArea area)
        {
            RectTransform b = Ui.Div(seg, "button", x, 0f, SegMinW, SegH);
            UiNode node = UiNode.Of(b);
            BoxGraphic box = BoxGraphic.On(b);
            float contentW = 0f;
            if (icon != null)
            {
                MetaUtil.Icon(b, icon, SegIcon, DsTokens.Ink);
                contentW += SegIcon + 8f;
            }
            TextStyle ts = MetaStyle.Text(SegButtonCss + SegButtonSpanCss);
            ts.WhiteSpace = WhiteSpace.NoWrap;
            Label.Make(b, text, ts, "span");
            contentW += TextMeasure.Width(text, ts);
            float w = Math.Max(SegMinW, contentW + 2f * SegPadX + 2f * Border);
            node.W = w;
            Ocb.Flow(b, MetaStyle.ButtonCss + SegButtonCss, null, w, SegH);
            states = CssStates.Of(b);
            states.Base(MetaStyle.Without(MetaStyle.ButtonCss + SegButtonCss, "width", "height", "min-width", "display"))
                .State(":hover", SegButtonHoverCss)
                .State(".is-on", SegButtonOnCss);
            states.Apply(instant: true);
            area = ClickArea.On(box);
            CssStates st = states;
            area.HoverChanged += on => { if (st) st.SetState(":hover", on); };
            return b;
        }

        // js: scenes/meta/settings.js:87
        /// <summary>
        /// A volume row: the track (its fill and knob at v), the number 0〜100. The value moves in steps of .05; a press
        /// sets it from where the pointer is on the track and a drag follows (the pointer is captured until it lifts).
        /// </summary>
        private void Slider(string label, Func<double> get, Action<double> store, Action<double> apply)
        {
            RectTransform row = MakeRow(label, out Action<bool> focus);
            float trackW = ContentW - ControlX - RowPadX - NumW - ColGap;    // the 1fr column (474)
            float innerW = trackW - 2f * Border;
            RectTransform track = Ui.Div(row, "ocbg-meta-slider", ControlX, RowPadY + (RowMinH - 2f * RowPadY - TrackH) / 2f, trackW, TrackH);
            UiNode.Of(track);
            BoxGraphic trackBox = BoxGraphic.On(track, MetaStyle.Without(SliderCss, "position", "height", "cursor", "touch-action"));
            trackBox.raycastTarget = true;
            // ::after: the track's padding box grown 14 px up and down (it takes the pointer for the track)
            RectTransform after = Ui.Div(track, "::after", Border, Border - AfterPad, innerW, TrackH - 2f * Border + 2f * AfterPad);
            Hit.Area(after);
            BoxGraphic fill = BoxGraphic.Make(track, MetaStyle.Without(SliderFillCss, "position", "left", "top", "bottom"), Border, Border, 0f, TrackH - 2f * Border, "ocbg-meta-slider-fill");
            UiNode fillNode = UiNode.Of(fill.rectTransform);
            BoxGraphic knob = BoxGraphic.Make(track, MetaStyle.Without(SliderKnobCss, "position", "top", "width", "height", "margin"), 0f, 0f, KnobSize, KnobSize, "ocbg-meta-slider-knob");
            knob.raycastTarget = true;   // a child of the track: a press on it is a press on the track
            UiNode knobNode = UiNode.Of(knob.rectTransform);
            TextStyle numStyle = MetaStyle.Text(SliderNumCss);
            numStyle.Align = TextAlign.Right;
            TextMeshProUGUI num = Label.Make(row, "", numStyle, ContentW - RowPadX - NumW, (RowMinH - numStyle.LineHeightPx) / 2f, NumW, numStyle.LineHeightPx, "ocbg-meta-slider-num");

            void Paint()
            {
                double v = get();
                fillNode.W = (float)(v * innerW);                                          // width: v%
                knobNode.Place(Border + (float)(v * innerW) - KnobSize / 2f, Border + (TrackH - 2f * Border) / 2f - KnobSize / 2f);   // left: v%, top: 50%, margin −12
                Label.SetText(num, MetaUtil.JsRound(v * 100).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            void SetV(double v, bool quiet = false)
            {
                v = Math.Max(0, Math.Min(1, MetaUtil.JsRound(v * 20) / 20.0));
                if (v == get()) return;
                store(v);
                try { apply(v); }
                catch (Exception) { /* no audio */ }
                Paint();
                if (!quiet) _sound.Play("uiClick");
            }

            void FromPointer(PointerEventData e)
            {
                if (e == null) return;
                TrialStage st = _ctx.Stage ? _ctx.Stage : TrialStage.Current;
                float bx;
                if (st) bx = Hit.ToBox(track, st.ToStage(e.position), st).x;
                else if (RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out Vector2 local)) bx = local.x - track.rect.xMin;
                else return;
                SetV(bx / trackW);   // (clientX − r.left) / r.width of the track's border box
            }

            SettingsSliderDrag drag = track.gameObject.AddComponent<SettingsSliderDrag>();
            drag.Down += FromPointer;
            drag.Move += FromPointer;
            Paint();
            _rows.Add(new Row { Left = () => SetV(get() - 0.05), Right = () => SetV(get() + 0.05), Paint = Paint, Focus = focus });
        }

        // js: scenes/meta/settings.js:131
        /// <summary>
        /// The footer: the note and 「保存して閉じる」 wherever there is something to save (ctx.canSaveQuit === false hides
        /// them), then 「閉じる」 + Esc. A fight is in the save too (tier1 R1-16, #462): every move is saved as it is made,
        /// so つづきから comes back to the same moment. In 継承の間 and 出立 ('lineage') there is no life yet: the button
        /// says it goes back to the title with everything saved.
        /// </summary>
        private void Footer()
        {
            // where Next puts the footer (its margin-top 16 after the column's gap); its height waits for the note
            float top = _y + (_first ? 0f : Gap) + FooterGap;
            BoxGraphic footer = BoxGraphic.Make(_panel, MetaStyle.Without(FooterCss, "display", "align-items", "justify-content", "gap", "margin-top"), X0, top, ContentW, Border + FooterGap + FooterButtonH, "footer");
            float by = top + Border + FooterGap;
            MetaButton closeBtn = MetaButton.Make(_panel, MetaButtonKind.Primary, MetaText.Close, "Esc", compact: true);
            closeBtn.SetBox(0f, 0f, null, FooterButtonH);
            float closeX = X0 + ContentW - closeBtn.Node.W;
            // the flex line is as tall as its tallest item (the 56 px buttons, or a note of more lines), each centred on it
            float lineH = FooterButtonH;
            if (_ctx.OnSaveQuit != null && _ctx.CanSaveQuit)
            {
                string kind = _ctx.SaveQuitKind ?? (_ctx.InBattle ? SaveQuitKinds.Battle : SaveQuitKinds.Life);
                MetaButton saveBtn = MetaButton.Make(_panel, MetaButtonKind.Btn, MetaText.SaveQuitOf(kind));
                saveBtn.SetBox(0f, 0f, null, FooterButtonH);
                float saveX = closeX - FooterGap - saveBtn.Node.W;
                saveBtn.Click += () =>
                {
                    _sound.Play("uiConfirm");
                    _ctx.OnSaveQuit();
                };
                // the note takes what the buttons leave, up to 300 px (flex: 1 1 auto; max-width: 300px; min-width: 0)
                TextStyle ns = MetaStyle.Text(NoteCss + FooterNoteCss);
                string note = MetaText.SaveNoteOf(kind);
                float nw = Math.Max(0f, Math.Min(FooterNoteMaxW, saveX - FooterGap - X0));
                float nh = Math.Max(ns.LineHeightPx, TextMeasure.Height(note, ns, Math.Max(1f, nw)));
                lineH = Math.Max(lineH, nh);
                saveBtn.Place(saveX, by + (lineH - FooterButtonH) / 2f);
                Label.Make(_panel, note, ns, X0, by + (lineH - nh) / 2f, nw, nh, "ocbg-meta-set-note");
                _rows.Add(new Row { Enter = () => saveBtn.Press(), Focus = on => saveBtn.SetClass("is-focus", FooterFocusCss, on) });
            }
            closeBtn.Place(closeX, by + (lineH - FooterButtonH) / 2f);
            Next(Border + FooterGap + lineH, FooterGap, 0f);
            UiNode.Of(footer.rectTransform).H = Border + FooterGap + lineH;
            closeBtn.Click += () => Close();
            // .ocbg-meta-primary:hover:not([disabled]) outranks footer .is-focus: while hovered, the hover's shadow wins
            bool closeFocused = false;
            void PaintCloseFocus() => closeBtn.SetClass("is-focus", FooterFocusCss, closeFocused && !closeBtn.Hovered);
            closeBtn.Area.HoverChanged += _ => PaintCloseFocus();
            _rows.Add(new Row
            {
                Enter = () => Close(),
                Focus = on =>
                {
                    closeFocused = on;
                    PaintCloseFocus();
                },
            });
        }

        // js: scenes/meta/settings.js:153
        private void PaintFocus()
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Focus?.Invoke(i == _focus);
        }

        // js: scenes/meta/settings.js:157
        /// <summary>
        /// audio.onChange(s =&gt; …) → unsubscribe. Trial.Meta does not reference Trial.Audio.Synth, where the listener's
        /// argument type (SoundSettings) lives, so the panel cannot name ITrialAudio.OnChange; it reads the audio's
        /// values on every frame of its clock instead and syncs when one moved (at most a frame after the change).
        /// </summary>
        private Action WatchAudio()
        {
            IDisposable frame = _clock.OnFrame((dt, now) =>
            {
                if (_audio.Master != _vol.Master || _audio.Effects != _vol.Effects || _audio.AmbienceVolume != _vol.Ambience
                    || _audio.Muted != _vol.Muted || _vol.Reverb != _audio.Reverb)
                    SyncFromAudio();
            });
            return () => frame.Dispose();
        }

        // js: scenes/meta/settings.js:157
        /// <summary>The change listener: Object.assign(vol, audio settings), then every row repaints.</summary>
        private void SyncFromAudio()
        {
            if (_audio == null || !_alive) return;
            _vol.Master = _audio.Master;
            _vol.Effects = _audio.Effects;
            _vol.Ambience = _audio.AmbienceVolume;
            _vol.Reverb = _audio.Reverb;
            _vol.Muted = _audio.Muted;
            foreach (Row r in _rows) r.Paint?.Invoke();
        }

        // js: scenes/meta/settings.js:166
        public void Close() => CloseAsync().Forget();

        private async Promise CloseAsync()
        {
            if (_closing) return;
            _closing = true;
            _sound.Play("uiCancel");
            _clock.Tween(_pd, new[] { ("o", 0f) }, 160f, Ease.In).Forget();
            await _clock.Tween(_ps, new[] { ("o", 0f), ("s", 0.98f), ("y", 6f) }, 160f, Ease.In);
            if (_alive) _ctx.OnClose?.Invoke();
        }

        // js: scenes/meta/settings.js:175
        private bool OnKey(KeyEvent e)
        {
            if (_closing) return true;
            // A held key repeats: only a fresh press closes, or the screen under it would open the panel again on the next
            // repeat, and close and open it over and over (its own openers ignore repeats too).
            if (e.Key == "Escape") { if (!e.Repeat) Close(); return true; }
            if (e.Key == "ArrowDown") { _focus = (_focus + 1) % _rows.Count; PaintFocus(); _sound.Play("nodeHover"); return true; }
            if (e.Key == "ArrowUp") { _focus = (_focus - 1 + _rows.Count) % _rows.Count; PaintFocus(); _sound.Play("nodeHover"); return true; }
            if (e.Key == "ArrowLeft") { _rows[_focus].Left?.Invoke(); return true; }
            if (e.Key == "ArrowRight") { _rows[_focus].Right?.Invoke(); return true; }
            if (e.Key == "Enter" || e.Key == " ")
            {
                Row r = _rows[_focus];
                if (r.Enter != null) r.Enter();
                else r.Right?.Invoke();
                return true;
            }
            return MetaUtil.ModalSwallow(e);   // modal: the screen under it gets no keys
        }

        // js: scenes/meta/settings.js:190
        public void Destroy()
        {
            _alive = false;
            _popKeys?.Invoke();
            _popKeys = null;
            _unsub?.Invoke();
            _unsub = null;
            _clock.Stop();
            Ui.Remove(_root);
        }

        /// <summary>settings.js has no refresh.</summary>
        public void Refresh() { }
    }

    /// <summary>
    /// The volume track's pointer (settings.js:113-120): pointerdown, then pointermove until pointerup while the pointer is
    /// captured (a uGUI drag stays with the pressed object; no drag threshold, so every move counts).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class SettingsSliderDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IDragHandler
    {
        public event Action<PointerEventData> Down;
        public event Action<PointerEventData> Move;
        private bool _held;

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            _held = true;
            Run(Down, e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_held) Run(Move, e);
        }

        public void OnPointerUp(PointerEventData e) => _held = false;

        private void OnDisable() => _held = false;

        private static void Run(Action<PointerEventData> handlers, PointerEventData e)
        {
            if (handlers == null) return;
            try { handlers(e); }
            catch (Exception ex) { Debug.LogException(ex); }
            Microtasks.Drain();
        }
    }
}
