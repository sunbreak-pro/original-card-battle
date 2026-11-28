// The base under the carry-over as one scene, 「降り口の前」 (#1052, the design canvas's 出立：降り口の前): the shared header
// band on top (#1074: 「拠点」, the collapse calendar, HP and stamina at full, and the owned cards, which open 「支度の卓」,
// the journal and the settings), the way down in the middle with the first layer's light through it and the explorer
// before it, the explorer's name and how far they have been on the left, the seven layers on the right with where the
// last descent ended, and at the foot the deck, the tools and the consumables (each opens 「支度の卓」, DepartureScene) and
// 「降りる」, held for DepartGate.HoldMs to set out with the pack as it stands. The list of past descents and the rules of
// traces are read in the journal (#1053), which the band's 手記 and J open. Back to the title is the settings'
// 「保存してタイトルへ」 (the router opens them with the lineage's save), so the base has no way of its own.
//
// Every box's place and size, the words and the record are Trial.Bridge.DepartGate (held under dotnet test); the pack is
// Trial.Bridge.DeparturePack, opened as the table opens it, so the table's draft comes back here and the choice is the
// table's. The generations' 継承の間 (the carry-over off) stays HallScene's.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Depiction.Bridge;
using InheritanceCore;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Header;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using UnityEngine.EventSystems;
using GateCss = Trial.Bridge.DepartGate.Css;
using TextMeasure = Trial.Kit.TextMeasure;

namespace Trial.Meta
{
    public sealed class GateScene : IScene
    {
        private readonly HallCtx _ctx;
        private readonly MetaSound _sound;
        private readonly Lineage _lineage;
        private readonly HallView _view;
        private readonly RectTransform _root;
        private readonly MetaBackdrop _backdrop;
        private readonly SceneClock _clock;
        private readonly Styler _fade;
        private readonly Action _popKeys;
        private bool _alive = true;
        private bool _busy;

        // The life about to set out (null while the lineage has one in progress: the router resumes it), the table's ctx
        // for it and the pack the table would open with.
        private readonly LifeStart _start;
        private readonly DepartureCtx _table;
        private readonly DeparturePack _pack;

        // 降りる
        private DepartureScene.DepButton _holdBtn;
        private RectTransform _holdFill;
        private bool _holding;
        private double _holdFrom;

        private GateScene(RectTransform host, HallCtx ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _sound = MetaUtil.SoundOf(ctx);
            _lineage = ctx.Lineage ?? throw new ArgumentNullException(nameof(ctx.Lineage));
            _view = Inheritance.HallView(_lineage);
            _start = _lineage.Current == null ? Inheritance.StartLife(_lineage, ctx.Seed) : null;
            _table = ctx.Departure?.Invoke(_start) ?? new DepartureCtx { LifeStart = _start };
            _pack = DepartureScene.PackOf(_table);

            _root = MetaUtil.SceneRoot(host, "gate");
            _backdrop = MetaBackdrop.Create(_root, HallBackdropSpec.Gate(), new MetaBackdrop.Options { Vignette = 0.45f });
            _clock = MetaUtil.SceneClock((dt, now) => { _backdrop.Paint(now); Frame(now); }, ctx.Stage);
            _backdrop.Alpha = 0f;

            DrawWayDown();
            List<RectTransform> left = DrawRecord();
            List<RectTransform> layers = DrawLayers();
            List<RectTransform> foot = DrawFoot();
            DrawHeader();

            // ---- arrival: the curtain opens, the wall comes up, the parts rise in ----
            _fade = Styler.Of(MetaStyle.Fade(_root), o: 1f);
            _sound.Play("hallArrive", 80);
            _clock.Tween(_fade, "o", 0f, 700f, Ease.Out);
            _clock.Tween(_backdrop, "v", 1f, 1200f, Ease.Out, 150f);
            MetaUtil.RiseIn(_clock, left, delay: 380f, step: 70f);
            MetaUtil.RiseIn(_clock, layers, delay: 440f, step: 50f, dy: 10f);
            MetaUtil.RiseIn(_clock, foot, delay: 620f, step: 70f);

            _popKeys = KeyStack.Push(OnKey);
            KeyStack.KeyUp += OnKeyUp;
            KeyStack.Blurred += StopHold;
        }

        /// <summary>The base under the carry-over (HallScene.Mount sends it here).</summary>
        public static IScene Mount(RectTransform host, HallCtx ctx) => new GateScene(host, ctx);

        // ================================================================================ the way down

        private void DrawWayDown()
        {
            // the floor in front of the way down
            TableBox floor = DepartGate.Floor;
            BoxGraphic.Make(_root, "background: linear-gradient(180deg, #0e1118, #07090d);", floor.X, floor.Y, floor.W, floor.H, "floor");

            // the way down: the first layer's light through it, its rim, and the rock's shelves fading into the light
            TableBox a = DepartGate.Arch;
            float r = a.W / 2f;
            string rim = "box-shadow: 0 0 0 " + Px(DepartGate.ArchRing) + " #262a36, 0 0 0 " + Px(DepartGate.ArchRing + DepartGate.ArchRim)
                + " var(--steel), 0 0 120px rgba(196,232,120,.35);";
            RectTransform arch = BoxGraphic.Make(_root, "background: " + HallParts.SkyCss(1) + "; border-radius: " + Px(r) + " " + Px(r) + " 0 0; " + rim,
                a.X, a.Y, a.W, a.H, "gate-way").rectTransform;
            foreach ((float x, float bottom, float w, float h, float alpha) in DepartGate.RockBands)
            {
                BoxGraphic.Make(arch, "background: rgba(" + DepartGate.RockRgb + "," + alpha.ToString("0.##", CultureInfo.InvariantCulture) + ");",
                    x, a.H - bottom - h, w, h, "rock");
            }

            // its light on the floor: a trapezoid fading downwards
            TableBox fl = DepartGate.FloorLight;
            float inset = fl.W * DepartGate.FloorLightInset;
            Rgba lit = Rgba.Bytes(196, 232, 120, 0.3f), none = Rgba.Bytes(196, 232, 120, 0f);
            VectorGraphic.Draw(_root, fl.X, fl.Y, fl.W, fl.H, (mb, box, feather) =>
            {
                int b = mb.VertexCount;
                mb.AddVertex(new Vec2(inset, 0f), lit);
                mb.AddVertex(new Vec2(fl.W - inset, 0f), lit);
                mb.AddVertex(new Vec2(fl.W, fl.H), none);
                mb.AddVertex(new Vec2(0f, fl.H), none);
                mb.AddTri(b, b + 1, b + 2);
                mb.AddTri(b, b + 2, b + 3);
            }, "gate-floorlight");

            // the explorer before it: the swordsman's silhouette with the player's foot light (the standing art is #84)
            TableBox e = DepartGate.Explorer, f = DepartGate.ExplorerFoot;
            GradientGraphic foot = Ocb.FootLight(_root, f.W, false);
            UiNode footNode = UiNode.Of(foot.rectTransform);
            footNode.Place(f.X, f.Y + f.H / 2f - footNode.H / 2f);
            PathGraphic fig = Ocb.Figure(_root, "swordsman", e.H - (e.Bottom - f.Y - f.H / 2f));
            UiNode figNode = UiNode.Of(fig.rectTransform);
            figNode.Place(e.X + (e.W - figNode.W) / 2f, f.Y + f.H / 2f - figNode.H);
        }

        // ================================================================================ the header band

        /// <summary>
        /// The shared band (#1074): the owned cards open the table as the foot's boxes do; the journal and the settings open
        /// as J and Esc do, and stand dimmed when the router gives no way to them.
        /// </summary>
        private void DrawHeader()
        {
            HeaderBar header = HeaderBar.Create(_root, DepartGate.BandOf(_lineage.Calendar, _pack.Deck.Total), new HeaderActions
            {
                OnOwnedCards = OpenTable,
                OnJournal = OpenJournal,
                OnSettings = OpenSettings,
            });
            if (_ctx.OnJournal == null) header.SetButton(HeaderButtonKind.Journal, HeaderButtonState.Off);
            if (_ctx.OnSettings == null) header.SetButton(HeaderButtonKind.Settings, HeaderButtonState.Off);
        }

        // ================================================================================ left: who goes down, how far they have been

        private List<RectTransform> DrawRecord()
        {
            var parts = new List<RectTransform>();
            HeirView heir = _view.NextHeir;
            TableBox who = DepartGate.WhoLabel, name = DepartGate.Name, rule = DepartGate.RecordRule;
            parts.Add(Label.Make(_root, DepartGate.WhoLine(_view.NextHeirHeading, heir.RoleLabel), MetaStyle.Text(GateCss.WhoLabel), who.X, who.Y, who.W, null, "who").rectTransform);
            TextStyle nameS = MetaStyle.Text(GateCss.Name);
            // a name the player wrote can run past the drawn names' four or five letters: it shrinks to the column
            float nw = TextMeasure.Width(heir.Name, nameS);
            if (nw > name.W && nw > 0f) nameS.Size = Math.Max(32f, (float)Math.Floor(nameS.Size * name.W / nw));
            parts.Add(Label.Make(_root, heir.Name, nameS, name.X, name.Y, null, null, "name").rectTransform);
            parts.Add(BoxGraphic.Make(_root, GateCss.RecordRule, rule.X, rule.Y, rule.W, rule.H, "rule").rectTransform);

            RectTransform rows = Ui.Div(_root, "ocbg-meta-gate-record", 0f, 0f, MetaUtil.W, MetaUtil.H);
            UiNode.Of(rows);
            parts.Add(rows);
            GateRecord r = DepartGate.RecordOf(_lineage);
            float y = DepartGate.RecordTop;
            if (r.Descents == 0)
            {
                // the first descent: the base's first-visit line, nothing to count yet
                TextStyle s = MetaStyle.P(GateCss.RecordValue);
                Label.Make(rows, _view.Empty, s, DepartGate.LeftX, y, DepartGate.LeftW, null, "empty");
                return parts;
            }
            y = RecordRow(rows, y, DepartGate.DescentsLabel, null, DepartGate.Times(r.Descents), r.Descents);
            if (r.Deepest.HasValue) y = RecordRow(rows, y, DepartGate.DeepestLabel, "第 ", " 層", r.Deepest.Value);
            if (r.LastLine.Length > 0) RecordRow(rows, y, DepartGate.LastLabel, null, r.LastLine, null);
            return parts;
        }

        /// <summary>
        /// One row of the record at y: the label, then the value — with <paramref name="number"/>, the number in the mono
        /// face between <paramref name="before"/> and the rest of <paramref name="text"/>'s words, on one baseline;
        /// without, <paramref name="text"/> wrapped to three lines. The next row's y.
        /// </summary>
        private static float RecordRow(RectTransform parent, float y, string label, string before, string text, int? number)
        {
            TextStyle ls = MetaStyle.Text(GateCss.RecordLabel), vs = MetaStyle.P(GateCss.RecordValue), ns = MetaStyle.Text(GateCss.RecordNumber);
            Label.Make(parent, label, ls, DepartGate.LeftX, y, DepartGate.RecordLabelW, null, "label");
            float x = DepartGate.RecordValueX, h = DepartGate.RecordLine;
            if (number.HasValue)
            {
                string num = number.Value.ToString(CultureInfo.InvariantCulture);
                string after = before == null ? text.Substring(num.Length) : text;
                float bv = TextMath.CssBaselineFromTop(vs.Spec), bn = TextMath.CssBaselineFromTop(ns.Spec), baseline = y + bn;
                if (!string.IsNullOrEmpty(before))
                {
                    Label.Make(parent, before, vs, x, baseline - bv, null, null, "before");
                    x += TextMeasure.Width(before, vs);
                }
                Label.Make(parent, num, ns, x, y, null, null, "number");
                x += TextMeasure.Width(num, ns);
                Label.Make(parent, after, vs, x, baseline - bv, null, null, "after");
            }
            else
            {
                vs.LineClamp = DepartGate.RecordMaxLines;
                h = Math.Min(DepartGate.RecordMaxLines * DepartGate.RecordLine, TextMeasure.Height(text, vs, DepartGate.RecordValueW));
                Label.Make(parent, text, vs, x, y, DepartGate.RecordValueW, h, "value");
            }
            return y + h + DepartGate.RecordRowGap;
        }

        // ================================================================================ right: the seven layers below

        private List<RectTransform> DrawLayers()
        {
            var parts = new List<RectTransform>();
            TableBox head = DepartGate.LayersHead;
            parts.Add(Label.Make(_root, DepartGate.LayersHeading, MetaStyle.Text(GateCss.LayersHeading), head.X, head.Y, head.W, null, "layers-head").rectTransform);
            TextStyle numS = MetaStyle.Text(GateCss.LayerNumber), textS = MetaStyle.Text(GateCss.LayerText);
            foreach (GateLayer l in DepartGate.LayersOf(_lineage))
            {
                TableBox b = DepartGate.Layer(l.Number);
                string css = GateCss.Layer + (l.Reached ? GateCss.LayerReached : "") + (l.King ? GateCss.LayerKing : "") + (l.Marked ? GateCss.LayerMarked : "");
                RectTransform box = BoxGraphic.Make(_root, css, b.X, b.Y, b.W, b.H, "layer " + l.Number).rectTransform;
                // a reached layer shows its light as a strip along the left edge (the words stay on the dark panel)
                if (l.Reached)
                    GradientGraphic.Css(box, HallParts.SkyCss(l.Number), DepartGate.LayerStripX, DepartGate.LayerPadY, DepartGate.LayerStripW, b.H - 2f * DepartGate.LayerPadY, false, "light");
                Label.Make(box, l.Number.ToString(CultureInfo.InvariantCulture), numS, DepartGate.LayerPadX, DepartGate.LayerPadY, null, null, "number");
                if (l.Text.Length > 0)
                {
                    Label.Make(box, l.Text, textS, DepartGate.LayerPadX, b.H - DepartGate.LayerPadY - DepartGate.LayerTextLine,
                        b.W - DepartGate.LayerPadX - DepartGate.LayerPadRight, null, "text");
                }
                parts.Add(box);
            }
            return parts;
        }

        // ================================================================================ the foot

        private List<RectTransform> DrawFoot()
        {
            var parts = new List<RectTransform>
            {
                DeckBox(DepartGate.Box(0)),
                SlotsBox(DepartGate.Box(1), "tools", DepartGate.ToolIcon, DepartTable.ToolsHeading(_pack.Supplies.Tools.Count, _pack.Limits.ToolSlots),
                    _pack.Supplies.Tools, _pack.Supplies.ToolOptions, _pack.Limits.ToolSlots),
                SlotsBox(DepartGate.Box(2), "consumables", DepartGate.ConsumableIcon, DepartGate.ConsumablesHeading(_pack.Supplies.Consumables.Count, _pack.Limits.ConsumableSlots),
                    _pack.Supplies.Consumables, _pack.Supplies.ConsumableOptions, _pack.Limits.ConsumableSlots),
                HoldBox(DepartGate.Hold),
            };
            return parts;
        }

        /// <summary>A box of the foot that opens the table: its frame, its hover, and the label row (icon and words).</summary>
        private DepartureScene.DepButton TableBoxButton(TableBox b, string name, string icon, string label)
        {
            var btn = new DepartureScene.DepButton(_root, "ocbg-meta-gate-box " + name, GateCss.Box, focusable: false).OnHover(GateCss.BoxHover).Done();
            btn.Node.Place(b.X, b.Y, b.W, b.H);
            btn.InlineStyler();   // the rise-in's opacity and lift are inline: the hover rule does not cut them
            btn.OnClick = OpenTable;
            TableBox inner = DepartGate.Inner(b);
            float ix = inner.X - b.X, iy = inner.Y - b.Y;
            PathGraphic g = PathGraphic.Icon(btn.Root, new IconDef(name, icon, IconDef.Box24, false), DepartGate.LabelIcon, DsTokens.Ink);
            Ui.Place(g.rectTransform, ix, iy + (DepartGate.LabelH - DepartGate.LabelIcon) / 2f);
            Label.Make(btn.Root, label, MetaStyle.Text(GateCss.BoxLabel), ix + DepartGate.LabelIcon + DepartGate.LabelIconGap, iy, null, DepartGate.LabelH, "label");
            return btn;
        }

        /// <summary>The deck: 「束」, the count and 「枚」 in amber with the life's rule, and 「組み直す」 at the bottom.</summary>
        private RectTransform DeckBox(TableBox b)
        {
            DepartureScene.DepButton btn = TableBoxButton(b, "deck", DepartGate.DeckIcon, DepartGate.DeckLabel);
            TableBox inner = DepartGate.Inner(b);
            float ix = inner.X - b.X, top = inner.Y - b.Y + DepartGate.LabelH + DepartGate.BoxGap;
            TextStyle countS = MetaStyle.Text(GateCss.DeckCount), unitS = MetaStyle.Text(GateCss.DeckUnit), ruleS = MetaStyle.Text(GateCss.DeckRule);
            string count = _pack.Deck.Total.ToString(CultureInfo.InvariantCulture);
            float baseline = top + TextMath.CssBaselineFromTop(countS.Spec);
            Label.Make(btn.Root, count, countS, ix, top, null, null, "count");
            float x = ix + TextMeasure.Width(count, countS) + 8f;
            Label.Make(btn.Root, "枚", unitS, x, baseline - TextMath.CssBaselineFromTop(unitS.Spec), null, null, "unit");
            x += TextMeasure.Width("枚", unitS) + 14f;
            Label.Make(btn.Root, DepartTable.RuleLine(_pack.Limits), ruleS, x, baseline - TextMath.CssBaselineFromTop(ruleS.Spec), null, null, "rule");

            TextStyle goS = MetaStyle.Text(GateCss.Go);
            float goW = TextMeasure.Width(DepartGate.Reassemble, goS) + 2f * DepartGate.GoPadX + 2f;
            RectTransform go = BoxGraphic.Make(btn.Root, GateCss.Go, ix, inner.Bottom - b.Y - DepartGate.GoH, goW, DepartGate.GoH, "go").rectTransform;
            Label.Make(go, DepartGate.Reassemble, goS, DepartGate.GoPadX + 1f, (DepartGate.GoH - goS.LineHeightPx) / 2f, null, null, "text");
            return btn.Root;
        }

        /// <summary>The tools or the consumables: 「道具 n / m」 and one slot a row, the thing's name or 「空き」.</summary>
        private RectTransform SlotsBox(TableBox b, string name, string icon, string label, IReadOnlyList<string> ids, IReadOnlyList<SupplyOption> options, int slots)
        {
            DepartureScene.DepButton btn = TableBoxButton(b, name, icon, label);
            TextStyle nameS = MetaStyle.Text(GateCss.SlotName), emptyS = MetaStyle.Text(GateCss.SlotEmptyWord);
            for (int i = 0; i < slots; i++)
            {
                TableBox s = DepartGate.Slot(b, i, slots);
                bool full = i < ids.Count;
                RectTransform slot = BoxGraphic.Make(btn.Root, full ? GateCss.Slot : GateCss.SlotEmpty, s.X - b.X, s.Y - b.Y, s.W, s.H, "slot").rectTransform;
                string word = full ? options.FirstOrDefault(o => o.Id == ids[i])?.Name ?? ids[i] : DepartGate.EmptySlot;
                TextStyle ts = full ? nameS : emptyS;
                Label.Make(slot, word, ts, DepartGate.SlotPadX, (s.H - ts.LineHeightPx) / 2f, s.W - 2f * DepartGate.SlotPadX, null, "name");
            }
            return btn.Root;
        }

        /// <summary>
        /// 「降りる」: held (Enter or the pointer) for DepartGate.HoldMs to set out, the bar filling as it is held. While the
        /// pack may not set out the button is dashed and says why (the deck's line), and a press opens nothing.
        /// </summary>
        private RectTransform HoldBox(TableBox b)
        {
            _holdBtn = new DepartureScene.DepButton(_root, "ocbg-meta-gate-descend", GateCss.Hold, focusable: false)
                .OnHover(GateCss.HoldHover)
                .OnClass("is-blocked", GateCss.HoldBlocked)
                .Done();
            _holdBtn.Node.Place(b.X, b.Y, b.W, b.H);
            _holdBtn.InlineStyler();
            _holdBtn.Toggle("is-blocked", !CanDescend);
            _holdBtn.Area.Down += e => { if (e == null || e.button == PointerEventData.InputButton.Left) StartHold(); };
            _holdBtn.Area.Up += _ => StopHold();
            _holdBtn.Area.Exit += _ => StopHold();

            TableBox word = DepartGate.HoldWord, hint = DepartGate.HoldHint, bar = DepartGate.HoldBar;
            TextStyle wordS = MetaStyle.Text(GateCss.HoldWord);
            wordS.Align = TextAlign.Center;
            Label.Make(_holdBtn.Root, DepartGate.Descend, wordS, 0f, word.Y - b.Y, b.W, word.H, "word");
            if (CanDescend)
            {
                // [Enter] を押し続ける, centred
                TextStyle keyS = MetaStyle.Text(GateCss.HoldKey), hintS = MetaStyle.Text(GateCss.HoldHint);
                float keyW = TextMeasure.Width(DepartGate.HoldKey, keyS) + 18f, hintW = TextMeasure.Width(DepartGate.HoldHintWords, hintS);
                float kx = (b.W - keyW - 10f - hintW) / 2f, ky = hint.Y - b.Y;
                RectTransform key = BoxGraphic.Make(_holdBtn.Root, GateCss.HoldKey, kx, ky + (hint.H - 24f) / 2f, keyW, 24f, "key").rectTransform;
                Label.Make(key, DepartGate.HoldKey, keyS, 9f, (24f - keyS.LineHeightPx) / 2f, null, null, "text");
                Label.Make(_holdBtn.Root, DepartGate.HoldHintWords, hintS, kx + keyW + 10f, ky, null, null, "hint");
            }
            else
            {
                TextStyle reasonS = MetaStyle.P(GateCss.HoldHint);
                reasonS.Align = TextAlign.Center;
                reasonS.LineClamp = DepartGate.HoldHintLines;
                string reason = _pack.Notice.Length > 0 ? _pack.Notice : _pack.Deck.StatusText;
                Label.Make(_holdBtn.Root, reason, reasonS, hint.X - b.X, hint.Y - b.Y, hint.W, hint.H * DepartGate.HoldHintLines, "reason");
            }
            if (CanDescend)
            {
                BoxGraphic.Make(_holdBtn.Root, GateCss.HoldBar, bar.X - b.X, bar.Y - b.Y, bar.W, bar.H, "bar");
                _holdFill = BoxGraphic.Make(_holdBtn.Root, GateCss.HoldFill, bar.X - b.X + 1f, bar.Y - b.Y + 1f, 0f, bar.H - 2f, "fill").rectTransform;
            }
            return _holdBtn.Root;
        }

        /// <summary>The pack may set out from here (a lineage with a life in progress goes on through the router).</summary>
        private bool CanDescend => _start == null || _pack.CanSetOut;

        // ================================================================================ holding 降りる

        private void StartHold()
        {
            if (_busy || _holding) return;
            if (!CanDescend)
            {
                _sound.Play("refusal");
                return;
            }
            _holding = true;
            _holdFrom = MetaUtil.PageNow;
        }

        private void StopHold()
        {
            if (!_holding || _busy) return;
            _holding = false;
            SetFill(0f);
        }

        // the scene clock's frame: the bar follows the hold in real time, and a full bar sets out
        private void Frame(double now)
        {
            if (!_holding || _busy) return;
            float p = DepartGate.HoldProgress(now - _holdFrom);
            SetFill(p);
            if (p >= 1f) Descend().Forget();
        }

        private void SetFill(float p)
        {
            if (_holdFill) UiNode.Of(_holdFill).W = Math.Max(0f, DepartGate.HoldBarW - 2f) * p;
        }

        // ================================================================================ the ways out

        /// <summary>
        /// The bar is full: uiConfirm, the curtain closes, and the life sets out with the pack as it stands, as the table's
        /// 出立する would (its draft goes: the router keeps the deck from here). A lineage with a life in progress hands null
        /// to the router, which resumes it.
        /// </summary>
        private async Promise Descend()
        {
            if (_busy) return;
            _busy = true;
            _holding = false;
            _sound.Play("uiConfirm");
            DepartureChoice choice = null;
            if (_start != null && _table.OnSetOut != null)
            {
                choice = _pack.Choice();
                DepartureScene.ClearDraft();
            }
            _clock.Tween(_backdrop, "v", 0.35f, 700f, Ease.InOut).Forget();
            await _clock.Tween(_fade, "o", 1f, 650f, Ease.In, 150f);
            if (!_alive) return;
            if (choice != null) _table.OnSetOut(choice);
            else _ctx.OnDepart?.Invoke(_start);
        }

        /// <summary>A box of the foot, or the band's owned cards: the table opens on the same pack (the router mounts 出立 with this life).</summary>
        private void OpenTable()
        {
            if (_busy) return;
            _busy = true;
            _holding = false;
            _sound.Play("uiClick");
            Leave(() => _ctx.OnDepart?.Invoke(_start)).Forget();
        }

        private async Promise Leave(Action then)
        {
            RectTransform curtain = MetaStyle.Fade(_root);
            Styler fade = Styler.Of(curtain, o: 0f);
            await _clock.Tween(fade, "o", 1f, 520f, Ease.In);
            if (_alive) then();
        }

        private void OpenSettings()
        {
            if (_busy || _ctx.OnSettings == null) return;
            StopHold();
            _sound.Play("uiClick");
            _ctx.OnSettings();
        }

        private void OpenJournal()
        {
            if (_busy || _ctx.OnJournal == null) return;
            StopHold();
            _ctx.OnJournal();
        }

        // ================================================================================ keys

        private bool OnKey(KeyEvent e)
        {
            if (_busy) return false;
            // a held key repeats: only a fresh press starts the hold or opens an overlay
            if (e.Key == "Enter")
            {
                if (!e.Repeat) StartHold();
                return true;
            }
            if (e.Key == "Escape" && _ctx.OnSettings != null)
            {
                if (!e.Repeat) OpenSettings();
                return true;
            }
            if ((e.Key == "j" || e.Key == "J") && _ctx.OnJournal != null)
            {
                if (!e.Repeat) OpenJournal();
                return true;
            }
            return false;
        }

        private void OnKeyUp(KeyEvent e)
        {
            if (e != null && e.Key == "Enter") StopHold();
        }

        private static string Px(float v) => v.ToString("0.##", CultureInfo.InvariantCulture) + "px";

        public void Destroy()
        {
            _alive = false;
            _popKeys?.Invoke();
            KeyStack.KeyUp -= OnKeyUp;
            KeyStack.Blurred -= StopHold;
            _clock.Stop();
            _backdrop.Destroy();
            Ui.Remove(_root);
        }

        /// <summary>An overlay closed over the base (the journal, 設定): a hold that was on when it opened has ended.</summary>
        public void Refresh() => StopHold();
    }
}
