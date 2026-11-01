// The player's stamina tag above the piles: a port of StaminaTagView (battle-visual-v1 §4.7, #242 / #455) in the design
// system's plate look (docs/BATTLE_SCENE.md §6c, battle-views §3.4). x 32, y 846, 236×84: the amber dot and the number (34) on
// the first row, with "▸ n" (wick, 24) on its right while a card is held; one dot per point of the maximum on the second, a
// divider after the stamina 構え needs (DepictionFrame.StanceDivider), the stamina there now in amber, the points a held card
// would pay as amber rings, the stamina the next turn start brings back as amber dashed rings (UnitFrame.NextRecovery), the
// rest as steel rings. It shows the numbers it is handed. In layer.hud. Port of scenes/battle/hud/stamina.js (+ hud.css:96-99);
// it is hud.stamina's { showCost, glowReserve } too.
//
// The DS rules (.ocb-stamina, .ocb-st-row1, .ocb-pips, .ocb-pip.is-*) are Ocb.*Css verbatim; the two flex rows run through the
// kit's flex solver (Ocb.Flow). The JS keeps its dot elements and re-appends the first `max` of them on every render (an inline
// scale / box-shadow survives a class change); here the same pips are kept, shown in order or hidden, and an inline shadow is
// laid over each restyle.
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>createStaminaTag(env) → { el, setStamina, setDivider, setNextRecovery, showCost, glowReserve, lightPip, popNumber, destroy }.</summary>
    // js: scenes/battle/hud/stamina.js:18
    public sealed class HudStaminaTag : IHudStamina
    {
        // StaminaTagView: At (32, 846), Size (236, 84). (layout.js L.STAMINA holds the same box.)
        /// <summary>"▸ n": the points a held card would pay (StaminaTagView.Render). scene text</summary>
        // js: scenes/battle/hud/stamina.js:12
        private const string PayMark = "▸ ";
        /// <summary>StaminaTagView: the amber dot before the number, 18×18.</summary>
        // js: scenes/battle/hud/stamina.js:14
        private const float Dot = 18f;
        /// <summary>DS 試作 (b): a point lighting swells 1.35 → 1 with an 8 px wick ring, over 320 ms.</summary>
        // js: scenes/battle/hud/stamina.js:16
        private const float LightMs = 320f;

        // ---- hud.css:96-99 and :29, verbatim ----

        /// <summary>hud.css:97 .ocbg-battle .ocbg-battle-stamina</summary>
        private const string TagCss = "position: absolute; box-sizing: border-box;";
        /// <summary>hud.css:98 .ocbg-battle-stamina .ocb-st-row1 b</summary>
        private const string NumCss = "font-variant-numeric: tabular-nums; transform-origin: 0 50%;";
        /// <summary>hud.css:99 .ocbg-battle-stamina .ocb-pip</summary>
        private const string PipCss = "box-sizing: border-box; transform-origin: 50% 50%;";
        /// <summary>hud.css:29 .ocbg-battle-dot</summary>
        private const string DotCss = "display: block; flex: none; border-radius: 50%;";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>One dot of the pip row: its element, its class (is-have / is-pay / is-next / is-empty) and its inline box-shadow.</summary>
        private sealed class Pip
        {
            public RectTransform El;
            public UiNode Node;
            public string Cls;
            public BoxShadow[] Shadow;
        }

        private readonly BattleViewEnv _env;
        private readonly RectTransform _el;
        private readonly RectTransform _row1;
        private readonly RectTransform _dot;
        private readonly TextMeshProUGUI _num;
        private readonly UiNode _numNode;
        private readonly TextMeshProUGUI _pay;
        private readonly UiNode _payNode;
        private readonly RectTransform _pips;
        private readonly RectTransform _divider;
        // js: scenes/battle/hud/stamina.js:36
        private readonly List<Pip> _dots = new List<Pip>();

        // js: scenes/battle/hud/stamina.js:35 st = { stamina, max, cost, next, divider }
        private int _stamina;
        private int _max;
        private int _cost;
        private int _next;
        private int _dividerAfter;

        // js: scenes/battle/hud/stamina.js:18
        private HudStaminaTag(BattleViewEnv env)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            // js: scenes/battle/hud/stamina.js:30 .ocb-stamina.ocbg-battle-stamina, at L.STAMINA in layer.hud
            _el = Ocb.El(env.Layer.Hud, "ocb-stamina ocbg-battle-stamina", Ocb.StaminaCss + TagCss);
            UiNode.Of(_el).Place(L.Stamina.X, L.Stamina.Y);
            // js: scenes/battle/hud/stamina.js:28 row 1: the dot, the number, 「▸ n」
            _row1 = Ocb.El(_el, "ocb-st-row1", Ocb.StRow1Css);
            // StaminaTagView: the dot is ProceduralArt.Circle at a full 18×18 (battle-visual-v1 §4.7 粒 18 px). Review fix: the 付録 A
            // pip icon is a radius-6 circle in its 24 box, so an 18 px icon drew a 9 px dot.
            // js: scenes/battle/hud/stamina.js:24
            _dot = Ocb.El(_row1, "ocbg-battle-dot", DotCss + "width: " + Ocb.Num(Dot) + "px; height: " + Ocb.Num(Dot) + "px; background: "
                + HudUtil.Tok("amber") + ";");
            Ocb.NoShrink(_dot);
            // js: scenes/battle/hud/stamina.js:20
            _num = Ocb.Text(_row1, "", Ocb.TextOf(Ocb.StaminaCss + Ocb.StRow1Css + Ocb.StRow1BCss + NumCss), "b");
            _numNode = UiNode.Of(_num.rectTransform).SetOrigin(0f, 0.5f);
            // js: scenes/battle/hud/stamina.js:21
            _pay = Ocb.Text(_row1, "", Ocb.TextOf(Ocb.StaminaCss + Ocb.StRow1Css + Ocb.StPayCss), "ocb-st-pay");
            Ocb.AutoMarginLeft(_pay);
            _payNode = UiNode.Of(_pay.rectTransform);
            _payNode.Opacity = Ocb.Rule(Ocb.StPayCss).Opacity ?? 0f; // .ocb-st-pay { opacity: 0 }
            // js: scenes/battle/hud/stamina.js:29
            _pips = Ocb.El(_el, "ocb-pips", Ocb.PipsCss);
            // js: scenes/battle/hud/stamina.js:37
            _divider = Ocb.El(_pips, "ocb-pip-div", Ocb.PipDivCss);
            Ocb.NoShrink(_divider);
            UiNode.Of(_divider).Visible = false;
            LayoutRows();
        }

        /// <summary>createStaminaTag(env): the tag at L.Stamina in layer.hud.</summary>
        // js: scenes/battle/hud/stamina.js:18
        public static HudStaminaTag Create(BattleViewEnv env) => new HudStaminaTag(env);

        /// <summary>StaminaTagView.Render.</summary>
        // js: scenes/battle/hud/stamina.js:40
        private void Render()
        {
            Label.SetText(_num, _stamina.ToString(Inv));
            Label.SetText(_pay, PayMark + _cost.ToString(Inv));
            // pay.classList.toggle('is-on', st.cost > 0): .ocb-st-pay.is-on { opacity: 1 }
            _payNode.Opacity = _cost > 0 ? (Ocb.Rule(Ocb.StPayCss + Ocb.StPayOnCss).Opacity ?? 1f) : (Ocb.Rule(Ocb.StPayCss).Opacity ?? 0f);
            while (_dots.Count < _max) _dots.Add(MakePip());
            // pips.innerHTML = ''
            UiNode.Of(_divider).Visible = false;
            foreach (Pip d in _dots) if (d.El) d.Node.Visible = false;
            int paying = Math.Min(_cost, _stamina);
            int order = 0;
            for (int i = 0; i < _max; i++)
            {
                // The divider stands in the gap after the stamina 構え needs, only when there are dots beyond it.
                if (_dividerAfter > 0 && i == _dividerAfter)
                {
                    UiNode.Of(_divider).Visible = true;
                    _divider.SetSiblingIndex(order++);
                }
                Pip dot = _dots[i];
                string cls = i < _stamina - paying ? "is-have" : i < _stamina ? "is-pay" : i < _stamina + _next ? "is-next" : "is-empty";
                SetClass(dot, cls);
                dot.Node.Visible = true;
                dot.El.SetSiblingIndex(order++);
            }
            LayoutRows();
        }

        /// <summary>h('i', { class: 'ocb-pip' }): a 16 px dot (flex: none), its class set by Render.</summary>
        // js: scenes/battle/hud/stamina.js:44
        private Pip MakePip()
        {
            RectTransform el = Ocb.El(_pips, "ocb-pip", Ocb.PipCss + PipCss);
            Ocb.NoShrink(el);
            return new Pip { El = el, Node = UiNode.Of(el) };
        }

        /// <summary>dot.className = 'ocb-pip ' + cls: the DS look of the class, with the dot's inline box-shadow kept over it.</summary>
        // js: scenes/battle/hud/stamina.js:52
        private static void SetClass(Pip dot, string cls)
        {
            if (dot.Cls == cls || !dot.El) return;
            dot.Cls = cls;
            Ocb.Restyle(dot.El, Ocb.PipCss + ClassCss(cls) + PipCss);
            ApplyShadow(dot);
        }

        /// <summary>The DS rule of a pip's class (bundle.css:131-134).</summary>
        private static string ClassCss(string cls)
        {
            switch (cls)
            {
                case "is-have": return Ocb.PipHaveCss;
                case "is-pay": return Ocb.PipPayCss;
                case "is-next": return Ocb.PipNextCss;
                default: return Ocb.PipEmptyCss;
            }
        }

        /// <summary>dot.style.boxShadow = shadow (null = '': the class's, which is none).</summary>
        private static void SetShadow(Pip dot, BoxShadow[] shadow)
        {
            dot.Shadow = shadow;
            ApplyShadow(dot);
        }

        private static void ApplyShadow(Pip dot)
        {
            if (!dot.El) return;
            BoxGraphic g = dot.El.GetComponent<BoxGraphic>();
            if (g) g.SetShadows(dot.Shadow);
        }

        /// <summary>
        /// The browser's layout of the two rows: row 1 (flex, centred, gap 8, 36 tall, the content width) at the padding's corner,
        /// the pips (flex, centred, gap 4, 20 tall) 8 px under it.
        /// </summary>
        private void LayoutRows()
        {
            BoxGraphic box = _el.GetComponent<BoxGraphic>();
            BoxStyle bs = box ? box.Style : Ocb.BoxOf(Ocb.StaminaCss + TagCss);
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            UiNode el = UiNode.Of(_el);
            float innerW = el.W - bw.Horizontal - pad.Horizontal;
            float x = bw.Left + pad.Left, y = bw.Top + pad.Top;
            Remeasure(_num);
            Remeasure(_pay);
            Ocb.Flow(_row1, Ocb.StRow1Css, null, innerW);
            UiNode row1 = UiNode.Of(_row1);
            row1.Place(x, y);
            Ocb.Flow(_pips, Ocb.PipsCss);
            float marginTop = Ocb.Px(Ocb.Rule(Ocb.PipsCss).Layout.MarginTop) ?? 8f;
            UiNode.Of(_pips).Place(x, y + row1.H + marginTop);
        }

        /// <summary>A label measured again from its text before a flex layout (the solver fixed its last box).</summary>
        private static void Remeasure(TMP_Text t)
        {
            LabelState st = Label.StateOf(t);
            st.AutoW = true;
            st.AutoH = true;
        }

        /// <summary>el.</summary>
        // js: scenes/battle/hud/stamina.js:58
        public RectTransform El => _el;

        /// <summary>setStamina(stamina, max).</summary>
        // js: scenes/battle/hud/stamina.js:59
        public void SetStamina(int stamina, int max)
        {
            _stamina = Math.Max(0, stamina);
            _max = Math.Max(0, max);
            Render();
        }

        /// <summary>setDivider(after): the divider after the stamina 構え needs (DepictionFrame.StanceDivider).</summary>
        // js: scenes/battle/hud/stamina.js:64
        public void SetDivider(int after)
        {
            int next = Math.Max(0, after);
            if (next == _dividerAfter) return;
            _dividerAfter = next;
            Render();
        }

        /// <summary>setNextRecovery(next): the stamina the next turn start brings back, as amber dashed rings (UnitFrame.NextRecovery).</summary>
        // js: scenes/battle/hud/stamina.js:70
        public void SetNextRecovery(int next)
        {
            int value = Math.Max(0, next);
            if (value == _next) return;
            _next = value;
            Render();
        }

        /// <summary>showCost(cost) (§4.7 / §5.2): while a card is held, the points it would pay turn to rings and 「▸ n」 shows; 0 puts both away.</summary>
        // js: scenes/battle/hud/stamina.js:77
        public void ShowCost(int cost)
        {
            _cost = Math.Max(0, cost);
            Render();
        }

        /// <summary>
        /// glowReserve(ms) (§5.13 順 1, #501): the dots up to the divider (構え's 息) glow once over `ms` (Ease.Out), swelling 25 %:
        /// box-shadow 0 0 (10 + 6a)px (4a)px amber at a, a = .9·sin πt. With no divider nothing glows.
        /// </summary>
        // js: scenes/battle/hud/stamina.js:85
        public Promise GlowReserve(float ms)
        {
            int count = Math.Min(_dividerAfter, _max);
            var lit = _dots.GetRange(0, Math.Max(0, Math.Min(count, _dots.Count)));
            return _env.Clock.Run(ms, Ease.Out, t =>
            {
                float a = 0.9f * (float)Math.Sin(t * Math.PI);
                float s = 1f + 0.25f * (float)Math.Sin(t * Math.PI);
                // '0 0 ' + round(10 + 6a) + 'px ' + round(4a) + 'px ' + mix('amber', round(a·100))
                var glow = new[] { new BoxShadow(0f, 0f, JsRound(10f + 6f * a), JsRound(4f * a), DsTokens.Amber.MulAlpha(JsRound(a * 100f) / 100f)) };
                foreach (Pip d in lit)
                {
                    if (!d.El) continue;
                    d.Node.PropScale = Fixed(s, 3);
                    SetShadow(d, glow);
                }
            }).Then(() =>
            {
                foreach (Pip d in lit)
                {
                    if (!d.El) continue;
                    d.Node.PropScale = 1f;
                    SetShadow(d, null);
                }
            });
        }

        /// <summary>lightPip(index): one recovered point lights (DS: a small swell 1.35 → 1 and an 8 px wick ring, 320 out), beside the recovery's own wait.</summary>
        // js: scenes/battle/hud/stamina.js:96
        public Promise LightPip(int index)
        {
            Pip dot = index >= 0 && index < _dots.Count ? _dots[index] : null;
            if (dot == null || !dot.El) return Promise.Resolved();
            return _env.Clock.Run(LightMs, Ease.Out, t =>
            {
                if (!dot.El) return;
                dot.Node.PropScale = Fixed(HudUtil.Lerp(1.35f, 1f, t), 3);
                // '0 0 0 ' + (8t).toFixed(1) + 'px ' + mix('wick', round(90(1 − t)))
                SetShadow(dot, new[] { new BoxShadow(0f, 0f, 0f, Fixed(8f * t, 1), DsTokens.Wick.MulAlpha(JsRound(90f * (1f - t)) / 100f)) });
            }).Then(() =>
            {
                if (!dot.El) return;
                dot.Node.PropScale = 1f;
                SetShadow(dot, null);
            });
        }

        /// <summary>popNumber(ms): a spend pops the number (StatusBarView pops the pip row Unity hides; this is the tag's).</summary>
        // js: scenes/battle/hud/stamina.js:105
        public Promise PopNumber(float ms) => HudUtil.Pop(_env.Clock, _numNode, ms);

        /// <summary>destroy().</summary>
        // js: scenes/battle/hud/stamina.js:106
        public void Destroy()
        {
            if (_el) Ui.Remove(_el);
        }

        /// <summary>Number.prototype.toFixed(digits) read back as a number (the style string the JS writes).</summary>
        private static float Fixed(float x, int digits) => (float)Math.Round((double)x, digits, MidpointRounding.AwayFromZero);

        /// <summary>Math.round (halves up).</summary>
        private static int JsRound(float x) => (int)Math.Floor(x + 0.5f);
    }
}
