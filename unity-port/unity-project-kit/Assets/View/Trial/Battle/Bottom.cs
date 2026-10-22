// The lower right of the battle screen (BottomRightViews, DepictionPlayer's end-turn plate; docs/BATTLE_SCENE.md §6c,
// battle-views §3.6): the played attributes (four 40×40 frames, count badges, the last-played underline, the 追撃 badge),
// the 構えの予告 (+3 with its shield and tip), and the end-turn plate (ターン終了, 40 % on the enemy's turn and at the end,
// Pop / Flash / Refuse). In layer.hud. The plate is `.ocbg-battle-endturn` (BattleHitTag.EndTurn: it counts as the arena
// while the flow runs). Port of scenes/battle/hud/bottom.js.
//
// - Played attributes: four 40×40 frames 6 px apart, 24 px in from the right at y 896, for 攻撃・防御・スキル・スタンス. An
//   attribute played this turn lights its icon in the attribute's colour and carries a 20 px count badge; one not played is
//   steel at 55 %. The attribute of the card played last is underlined. While a 追撃 waits, a fifth frame left of them reads
//   "+5" in phosphor (it pops in 150 ms and fades in 150 ms).
// - The 構えの予告 at x 1560, y 984, 56 tall: a Guard-coloured 1 px dashed frame, a 30 px shield and "+3".
// - The end-turn plate at x 1672, y 976, 224×72: amber 2 px frame over amber 16 %, its label at 24 px; 40 % while the enemy
//   has the turn (EnemyTurnShadeLayout.EndTurnAlpha).
// The CSS strings are hud.css's (and the DS's Ocb.*Css) verbatim, composed in cascade order.
using System;
using System.Collections.Generic;
using System.Globalization;
using Depiction;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    // js: scenes/battle/hud/bottom.js
    public static class HudBottom
    {
        /// <summary>
        /// ReserveTip(): the hint's hover line names the rule it shows — the glossary's 構え (CardFaceText.Glossary) — and that
        /// ending the turn now gives it: 「構え：…。いまターンを終えると付く」.
        /// </summary>
        // js: scenes/battle/hud/bottom.js:22
        public static string ReserveTip() =>
            "構え：" + (global::Depiction.Bridge.CardFaceText.Glossary.TryGetValue("構え", out string meaning) ? meaning : "undefined")
            + "。いまターンを終えると付く";

        /// <summary>END_TURN_LABEL: the plate's label (the Unity prefab's Text; DS EndTurnButton). scene text</summary>
        // js: scenes/battle/hud/bottom.js:25
        public const string EndTurnLabel = "ターン終了";
    }

    /// <summary>createPlayed(env) → { bind(played, animate), destroy }.</summary>
    // js: scenes/battle/hud/bottom.js:27
    public sealed class HudPlayed
    {
        /// <summary>.ocbg-battle-attr.ocb-attr (hud.css:127).</summary>
        private const string AttrHudCss = "position: absolute; box-sizing: border-box;";
        /// <summary>.ocbg-battle-attr .ocb-count (hud.css:129).</summary>
        private const string CountHudCss = "box-sizing: border-box; font-weight: 700; color: var(--ink);";
        /// <summary>.ocbg-battle-attr-last (hud.css:130).</summary>
        private const string LastCss = "position: absolute; left: 4px; right: 4px; bottom: -7px; height: 2px;";
        /// <summary>.ocbg-battle-followup (hud.css:131) on the DS .ocb-panel.</summary>
        private const string FollowCss =
            "position: absolute; box-sizing: border-box; min-width: 40px; height: 40px; padding: 0 6px; display: flex; align-items: center; "
            + "justify-content: center; transform-origin: 100% 0;";
        /// <summary>.ocbg-battle-followup b (hud.css:132).</summary>
        private const string FollowBCss = "font: 700 24px/1 var(--font-mono); color: var(--phosphor); white-space: nowrap;";

        private sealed class Slot
        {
            public CardKind Kind;
            public HudKindLook Look;
            public RectTransform El;
            public RectTransform IconBox;
            public RectTransform Badge;
            public TextMeshProUGUI BadgeText;
            public RectTransform Underline;
            /// <summary>lit: null before the first bind.</summary>
            public bool? Lit;
        }

        private readonly BattleViewEnv _env;
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly RectTransform _follow;
        private readonly TextMeshProUGUI _followText;
        private int _followShown;
        private int _followToken;
        private bool _shownAll;

        // js: scenes/battle/hud/bottom.js:27
        private HudPlayed(BattleViewEnv env)
        {
            _env = env;
            RectTransform hud = env.Layer.Hud;
            for (int i = 0; i < PlayedLayout.Order.Length; i++)
            {
                CardKind kind = PlayedLayout.Order[i];
                HudKindLook look = HudUtil.KindLook[kind];
                string css = SlotCss(false);
                RectTransform el = Ocb.El(hud, "ocb-panel ocb-attr ocbg-battle-attr", css);
                UiNode node = UiNode.Of(el);
                node.Place(PlayedLayout.LeftOf(i), PlayedLayout.Top);
                BoxStyle bs = Ocb.BoxOf(css);
                Insets bw = bs.BorderWidths;
                float cw = node.W - bw.Horizontal, ch = node.H - bw.Vertical;
                // the icon's box (display: flex) centred in the frame (justify-content / align-items: center)
                RectTransform iconBox = Ui.Div(el, "ocbg-battle-attr-icon");
                UiNode.Of(iconBox).Place(bw.Left + (cw - PlayedLayout.IconSize) * 0.5f, bw.Top + (ch - PlayedLayout.IconSize) * 0.5f,
                    PlayedLayout.IconSize, PlayedLayout.IconSize);
                // the count badge: position absolute, right −8, top −8 of the frame's padding box; its border in the attribute's frame colour
                string badgeCss = Ocb.AttrCountCss + CountHudCss + "border-color: " + look.Frame + ";";
                RectTransform badge = Ocb.El(el, "ocb-count ocbg-battle-attr-count", badgeCss);
                UiNode bn = UiNode.Of(badge);
                Insets cb = Ocb.BoxOf(badgeCss).BorderWidths;
                bn.Place(node.W - bw.Right + 8f - bn.W, bw.Top - 8f);
                TextStyle bts = Ocb.TextOf(badgeCss);
                TextMeshProUGUI badgeText = Ocb.Text(badge, "", bts, cb.Left, cb.Top, bn.W - cb.Horizontal, bts.LineHeightPx, TextAlign.Center, "text");
                // the last-played underline: left 4, right 4, bottom −7 of the padding box, 2 tall, in the frame colour
                RectTransform underline = Ocb.El(el, "ocbg-battle-attr-last", LastCss + "background: " + look.Frame + ";");
                UiNode.Of(underline).Place(bw.Left + 4f, node.H - bw.Bottom + 7f - 2f, cw - 8f, 2f);
                _slots.Add(new Slot
                {
                    Kind = kind, Look = look, El = el, IconBox = iconBox, Badge = badge, BadgeText = badgeText, Underline = underline,
                });
            }
            string followCss = Ocb.PanelCss + FollowCss;
            _follow = Ocb.El(hud, "ocb-panel ocbg-battle-followup", followCss);
            _followText = Ocb.Text(_follow, "", Ocb.TextOf(followCss + FollowBCss), "b");
            UiNode fnode = UiNode.Of(_follow);
            // It hangs from its right edge, so a wider number grows away from the row (PlayedLayout.FollowUpRight).
            fnode.SetOrigin(1f, 0f);   // transform-origin: 100% 0
            LayoutFollow();
            fnode.Visible = false;
        }

        private static string SlotCss(bool on) => Ocb.PanelCss + Ocb.AttrCss + AttrHudCss + (on ? Ocb.AttrOnCss : "");

        /// <summary>The badge's box: right edge at FollowUpRight, top 896, min 40 wide, its number centred.</summary>
        private void LayoutFollow()
        {
            string followCss = Ocb.PanelCss + FollowCss;
            BoxStyle bs = Ocb.BoxOf(followCss);
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            TextStyle ts = Ocb.TextOf(followCss + FollowBCss);
            float tw = Ocb.TextWidth(Label.StateOf(_followText).Text ?? "", ts);
            float w = Math.Max(40f, tw + pad.Horizontal + bw.Horizontal), h = 40f;
            UiNode.Of(_follow).Place(PlayedLayout.FollowUpRight - w, PlayedLayout.Top, w, h);
            Ocb.Flow(_follow, followCss, null, w, h);
        }

        /// <summary>createPlayed(env): the four frames at PlayedLayout and the follow-up badge, in layer.hud.</summary>
        // js: scenes/battle/hud/bottom.js:27
        public static HudPlayed Create(BattleViewEnv env) => new HudPlayed(env);

        // js: scenes/battle/hud/bottom.js:53
        private void BindFollowUp(int n, bool animate)
        {
            if (!_follow) return;
            int before = _followShown;
            _followShown = n;
            if (n == before) return;
            int token = ++_followToken;
            UiNode follow = UiNode.Of(_follow);
            if (n > 0)
            {
                Label.SetText(_followText, PlayedLayout.FollowUpLabel(n));
                LayoutFollow();
                follow.Visible = true;
                follow.Opacity = 1f;
                follow.PropScale = 1f;
                if (animate && before <= 0) HudUtil.ScaleTo(_env.Clock, follow, 0.6f, 1f, PlayedLayout.FollowUpPopMs, Ease.Out).Forget();
                return;
            }
            if (!animate || !follow.Visible)
            {
                follow.Visible = false;
                return;
            }
            _env.Clock.Run(PlayedLayout.FollowUpVanishMs, Ease.Linear, t =>
                {
                    if (token == _followToken && follow) follow.Opacity = HudChrome.Fixed(1f - t, 3);
                })
                .Then(() =>
                {
                    if (token == _followToken && _followShown <= 0 && follow) follow.Visible = false;
                }).Forget();
        }

        /// <summary>bind(played, animate) = PlayedAttributesView.Bind; `animate` false on a battle's opening frame.</summary>
        // js: scenes/battle/hud/bottom.js:73
        public void Bind(PlayedFrame played, bool animate)
        {
            _shownAll = played != null && played.Shown;
            foreach (Slot s in _slots) if (s.El) UiNode.Of(s.El).Visible = _shownAll;
            BindFollowUp(_shownAll ? played.FollowUp : 0, animate);
            if (!_shownAll) return;
            foreach (Slot s in _slots)
            {
                if (!s.El) continue;
                int count = played.CountOf(s.Kind);
                bool lit = count > 0;
                if (s.Lit != lit)
                {
                    s.Lit = lit;
                    Ui.Clear(s.IconBox);
                    Ocb.Icon(s.IconBox, s.Look.Icon, PlayedLayout.IconSize, lit ? s.Look.InkRgba : DsTokens.Steel);
                    // .is-on: opacity 1 (else --opacity-unplayed)
                    Ocb.Restyle(s.El, SlotCss(lit), null, true);
                }
                UiNode.Of(s.Badge).Visible = lit;
                Label.SetText(s.BadgeText, count.ToString(CultureInfo.InvariantCulture));
                UiNode.Of(s.Underline).Visible = lit && played.Last.HasValue && played.Last.Value == s.Kind;
            }
        }

        // js: scenes/battle/hud/bottom.js:92
        public void Destroy()
        {
            foreach (Slot s in _slots) if (s.El) Ui.Remove(s.El);
            if (_follow) Ui.Remove(_follow);
        }
    }

    /// <summary>createReserveHint(env, tip) → { show(hint), destroy }.</summary>
    // js: scenes/battle/hud/bottom.js:96
    public sealed class HudReserveHint
    {
        /// <summary>.ocbg-battle-hint (hud.css:133).</summary>
        private const string HintCss =
            "position: absolute; box-sizing: border-box; display: flex; align-items: center; gap: 8px; padding: 0 12px; "
            + "background: var(--panel); border: var(--stroke-rule) dashed var(--guard); border-radius: var(--radius-panel);";
        /// <summary>.ocbg-battle-hint b (hud.css:134).</summary>
        private const string HintBCss = "font: 700 24px/1 var(--font-mono); color: var(--guard); white-space: nowrap;";

        private readonly BattleViewEnv _env;
        private readonly HudTip _tip;
        private readonly RectTransform _el;
        private readonly TextMeshProUGUI _text;

        // js: scenes/battle/hud/bottom.js:96
        private HudReserveHint(BattleViewEnv env, HudTip tip)
        {
            _env = env;
            _tip = tip;
            _el = Ocb.El(env.Layer.Hud, "ocbg-battle-hint" + (tip != null ? " ocbg-battle-hit" : ""), HintCss);
            Ocb.Icon(_el, "shield", ReserveHintLayout.ShieldSize, DsTokens.Guard, DsTokens.Guard, 0.2f);
            _text = Ocb.Text(_el, "", Ocb.TextOf(HintCss + HintBCss), "b");
            if (tip != null)
            {
                // the line opens above the hint, right edges aligned (the end-turn plate and the screen's edge are below and right)
                ClickArea area = ClickArea.On(_el);
                BattleHitTag.Mark(_el);
                area.Enter += _ => _tip.Show(HudUtil.LineTip(HudBottom.ReserveTip()), n =>
                {
                    UiNode nn = UiNode.Of(n);
                    nn.Place(ReserveHintLayout.Left + ReserveHintLayout.Width - nn.W, ReserveHintLayout.Top - nn.H - 8f);
                }, this);
                area.Exit += _ => _tip.Hide(this);
            }
            UiNode.Of(_el).Place(ReserveHintLayout.Left, ReserveHintLayout.Top, ReserveHintLayout.Width, ReserveHintLayout.Height);
            Layout();
            UiNode.Of(_el).Visible = false;
        }

        private void Layout() => Ocb.Flow(_el, HintCss, null, ReserveHintLayout.Width, ReserveHintLayout.Height);

        /// <summary>createReserveHint(env, tip): the hint at ReserveHintLayout (hidden); with a tip it is a hit and shows ReserveTip on hover.</summary>
        // js: scenes/battle/hud/bottom.js:96
        public static HudReserveHint Create(BattleViewEnv env, HudTip tip = null) => new HudReserveHint(env, tip);

        /// <summary>show(hint) = ReserveHintView.Show: 「+3」 with its shield, or nothing for ''.</summary>
        // js: scenes/battle/hud/bottom.js:117
        public void Show(string hint)
        {
            bool on = !string.IsNullOrEmpty(hint);
            if (_el) UiNode.Of(_el).Visible = on;
            if (on)
            {
                Label.SetText(_text, hint);
                Layout();
            }
            else _tip?.Hide(this);
        }

        // js: scenes/battle/hud/bottom.js:123
        public void Destroy()
        {
            _tip?.Hide(this);
            if (_el) Ui.Remove(_el);
        }
    }

    /// <summary>createEndTurn(env, onClick) → { el, setEnemyTurn, setOff, pop, flash, refuse, box, destroy }. It is hud.endTurn too.</summary>
    // js: scenes/battle/hud/bottom.js:131
    public sealed class HudEndTurn : IHudEndTurn
    {
        /// <summary>FLASH_BRIGHT: the end-turn plate's one brightening (§3.5) — how much brighter at its peak.</summary>
        // js: scenes/battle/hud/bottom.js:128
        private const float FlashBright = 0.6f;
        /// <summary>FLASH_GLOW: its amber light's reach.</summary>
        // js: scenes/battle/hud/bottom.js:129
        private const float FlashGlow = 24f;

        /// <summary>.ocbg-battle-endturn.ocb-endturn (hud.css:135) on the DS .ocb-endturn.</summary>
        private const string EndTurnHudCss = "position: absolute; box-sizing: border-box; transform-origin: 50% 50%; user-select: none;";
        /// <summary>.ocbg-battle-endturn.ocb-endturn.is-off:hover (hud.css:136).</summary>
        private const string OffHoverCss = "background: color-mix(in srgb, var(--amber) 16%, transparent);";
        /// <summary>.ocbg-battle-endturn.ocb-endturn.is-over (and :hover) (hud.css:138-139).</summary>
        private const string OverCss = "border-color: var(--steel); background: transparent;";

        private static readonly string ElCss = Ocb.EndTurnCss + EndTurnHudCss;

        private readonly BattleViewEnv _env;
        private readonly Action _onClick;
        private readonly RectTransform _el;
        private readonly UiNode _node;
        private bool _enemyTurn;
        private bool _over;
        private bool _hover;
        /// <summary>el.style.boxShadow (the flash's amber light), or null.</summary>
        private string _shadow;

        // js: scenes/battle/hud/bottom.js:131
        private HudEndTurn(BattleViewEnv env, Action onClick)
        {
            _env = env;
            _onClick = onClick;
            _el = Ocb.El(env.Layer.Hud, "ocb-endturn ocbg-battle-endturn ocbg-battle-hit", ElCss);
            _node = UiNode.Of(_el);
            _node.Place(L.EndTurn.X, L.EndTurn.Y);
            BoxStyle bs = Ocb.BoxOf(ElCss);
            Insets bw = bs.BorderWidths;
            TextStyle ts = Ocb.TextOf(ElCss);
            float tw = Ocb.TextWidth(HudBottom.EndTurnLabel, ts);
            // display: flex, centred: the text item (its trailing letter-spacing included) in the middle of the content box
            Label.Make(_el, HudBottom.EndTurnLabel, ts, (_node.W - tw) * 0.5f, bw.Top + (_node.H - bw.Vertical - ts.LineHeightPx) * 0.5f, null, null, "text");
            ClickArea area = ClickArea.On(_el);
            BattleHitTag.Mark(_el, hit: true, endTurn: true);
            area.HoverChanged += on =>
            {
                _hover = on;
                Restate();
            };
            area.Click += _ => { if (_onClick != null && !_over) _onClick(); };
        }

        /// <summary>The plate's declarations: the DS plate, :hover, .is-off (and its :hover), .is-over, the inline box-shadow.</summary>
        private string Css()
        {
            bool off = _enemyTurn || _over;
            string css = ElCss;
            if (_hover) css += Ocb.EndTurnHoverCss;
            if (off) css += Ocb.EndTurnOffCss;
            if (off && _hover) css += OffHoverCss;
            if (_over) css += OverCss;
            if (_shadow != null) css += "box-shadow: " + _shadow + ";";
            return css;
        }

        private void Restate()
        {
            if (_el) Ocb.Restyle(_el, Css());
        }

        // js: scenes/battle/hud/bottom.js:140
        private void Draw()
        {
            if (!_node) return;
            bool off = _enemyTurn || _over;
            _node.Opacity = off ? EnemyTurnShadeLayout.EndTurnAlpha : 1f;
            Restate();
        }

        /// <summary>createEndTurn(env, onClick): the plate at L.EndTurn in layer.hud; a click calls onClick unless the battle is over.</summary>
        // js: scenes/battle/hud/bottom.js:131
        public static HudEndTurn Create(BattleViewEnv env, Action onClick) => new HudEndTurn(env, onClick);

        /// <summary>el.</summary>
        // js: scenes/battle/hud/bottom.js:147
        public RectTransform El => _el ? _el : null;

        /// <summary>setEnemyTurn(on): ShowEnemyTurn's plate half — 40 % while the enemy acts.</summary>
        // js: scenes/battle/hud/bottom.js:149
        public void SetEnemyTurn(bool on)
        {
            _enemyTurn = on;
            Draw();
        }

        /// <summary>setOff(on): the battle is over — the plate is off for good, at 40 %, steel, no click.</summary>
        // js: scenes/battle/hud/bottom.js:154
        public void SetOff(bool on)
        {
            _over = on;
            Draw();
        }

        /// <summary>pop(ms) — Reserve: UiTween.Pop(endTurn, ms).</summary>
        // js: scenes/battle/hud/bottom.js:159
        public Promise Pop(float ms) => HudUtil.Pop(_env.Clock, _node ? _node : null, ms);

        /// <summary>
        /// flash(ms) — battle_ui_ux_v2 §3.5 「出せる札が 1 枚も無い … ターン終了ボタンが 1 回だけ明るくなる」 (review 16): the plate
        /// brightens once and settles over `ms` (out), an amber light around it.
        /// </summary>
        // js: scenes/battle/hud/bottom.js:164
        public Promise Flash(float ms)
        {
            if (_over || _enemyTurn) return Promise.Resolved();
            return _env.Clock.Run(ms, Ease.Out, t =>
            {
                if (!_node) return;
                float k = (float)Math.Sin(t * Math.PI);
                _node.Filter = t < 1f ? new ColorFilter(HudChrome.Fixed(1f + FlashBright * k, 3)) : ColorFilter.None;
                _shadow = t < 1f
                    ? "0 0 " + JsRound(FlashGlow * k).ToString(CultureInfo.InvariantCulture) + "px color-mix(in srgb, var(--amber) "
                        + JsRound(70f * k).ToString(CultureInfo.InvariantCulture) + "%, transparent)"
                    : null;
                Restate();
            });
        }

        /// <summary>
        /// refuse(ms) — review 21: a press the mash window swallowed (player.requestEndTurn) is seen: the plate shakes 6 px twice
        /// over `ms`, as a refused card does (ReturnToHand's shake), and dims for that moment.
        /// </summary>
        // js: scenes/battle/hud/bottom.js:176
        public Promise Refuse(float ms)
        {
            if (_over || _enemyTurn) return Promise.Resolved();
            if (_node) _node.Filter = new ColorFilter(0.75f);
            return HudUtil.Shake(_env.Clock, _node ? _node : null, 6f, 2f, ms).Then(() =>
            {
                if (_node) _node.Filter = ColorFilter.None;
            });
        }

        /// <summary>box(): L.EndTurn.</summary>
        // js: scenes/battle/hud/bottom.js:181
        public StageBox Box() => new StageBox(L.EndTurn.X, L.EndTurn.Y, L.EndTurn.W, L.EndTurn.H);

        // js: scenes/battle/hud/bottom.js:182
        public void Destroy()
        {
            if (_el) Ui.Remove(_el);
        }

        /// <summary>Math.round: halves go up.</summary>
        private static int JsRound(float x) => (int)Math.Floor(x + 0.5);
    }
}
