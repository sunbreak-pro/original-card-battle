// The supplies of 出立's one table (#1051, the design canvas's 出立：支度の卓): the shelf of tools and consumables under the
// owned cards, and the pack's tool and consumable slots. The JS's 出立の支度 step (scenes/meta/departure.js:509-658) drew
// them as rows of text with − ×n ＋; here each thing stands on the shelf as itself (its line art until the painted art
// comes) and goes into the pack when pressed; a full slot takes it out again. The start-position diagram moved into the
// reading panel under 間合いの履 (DepartureScene.ReadItem). The screen, the cards, the pack's pile and the keys are
// DepartureScene.cs; what the supply items are and what a built Departure hands on is Trial.Bridge.DepartureCarry (the
// port of DepartureSupplies.js); every box's place is Trial.Bridge.DepartTable.
//
// Every word, count and verdict comes from DepartureBuilder (the life's limits) and DepartTable; nothing here decides.
using System;
using System.Collections.Generic;
using System.Globalization;
using Depiction.Bridge;
using TMPro;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using DepButton = Trial.Meta.DepartureScene.DepButton;
using TableCss = Trial.Bridge.DepartTable.Css;

namespace Trial.Meta
{
    internal sealed class DepartureSuppliesView
    {
        private const string Tool = "tool", Consumable = "consumable";

        private readonly DepartureScene _s;
        private readonly RectTransform _root, _toolSlotRow, _consSlotRow;
        private readonly List<ShelfItem> _items = new List<ShelfItem>();
        private readonly List<DepButton> _toolSlots = new List<DepButton>();
        private readonly List<DepButton> _consSlots = new List<DepButton>();
        private readonly TextMeshProUGUI _toolHead, _consHead;

        /// <summary>One thing on the shelf: its button (the whole column), its socket and its count.</summary>
        private sealed class ShelfItem
        {
            public SupplyOption Option;
            public string Kind;
            public DepButton Button;
            public CssStates Socket;
            public TextMeshProUGUI Count;
        }

        internal DepartureSuppliesView(DepartureScene scene, RectTransform root)
        {
            _s = scene ?? throw new ArgumentNullException(nameof(scene));
            _root = root ?? throw new ArgumentNullException(nameof(root));
            DepartureBuilder supplies = _s.Supplies;

            // ---- the shelf: 品の棚 and what to do, then the tools, a divider, the consumables ----
            TableBox head = DepartTable.ShelfHead;
            TextStyle hs = MetaStyle.Text(TableCss.ShelfHeading), ls = MetaStyle.Text(TableCss.ShelfLead);
            float baseline = head.Y + TextMath.CssBaselineFromTop(hs.Spec);
            float hw = TextMeasure.Width(DepartTable.ShelfHeading, hs);
            Label.Make(_root, DepartTable.ShelfHeading, hs, head.X, head.Y, null, null, "h2");
            Label.Make(_root, DepartTable.ShelfLead, ls, head.X + hw + 16f, baseline - TextMath.CssBaselineFromTop(ls.Spec), null, null, "lead");

            int tools = supplies.ToolOptions.Count, cons = supplies.ConsumableOptions.Count;
            for (int i = 0; i < tools; i++) _items.Add(MakeItem(supplies.ToolOptions[i], Tool, DepartTable.ShelfItem(i, tools, cons)));
            TableBox divider = DepartTable.ShelfDivider(tools, cons);
            BoxGraphic.Make(_root, "background: var(--rule);", divider.X, divider.Y, divider.W, divider.H, "divider");
            for (int i = 0; i < cons; i++) _items.Add(MakeItem(supplies.ConsumableOptions[i], Consumable, DepartTable.ShelfItem(tools + i, tools, cons)));

            // ---- the pack's slots, each row under its heading ----
            TextStyle sh = MetaStyle.Text(TableCss.SlotHead);
            _toolHead = Label.Make(_root, "", sh, DepartTable.ToolHead.X, DepartTable.ToolHead.Y, null, null, "h4");
            _toolSlotRow = Ui.Div(_root, "ocbg-meta-slots", 0f, 0f, MetaUtil.W, MetaUtil.H);
            UiNode.Of(_toolSlotRow);
            _consHead = Label.Make(_root, "", sh, DepartTable.ConsumableHead.X, DepartTable.ConsumableHead.Y, null, null, "h4");
            _consSlotRow = Ui.Div(_root, "ocbg-meta-slots", 0f, 0f, MetaUtil.W, MetaUtil.H);
            UiNode.Of(_consSlotRow);
        }

        /// <summary>
        /// One thing on the shelf at <paramref name="box"/>: the socket (62, its line art 30 inside, the amber ring while it is
        /// in the pack), its name under it in two lines at most, and ×n at the socket's top right while it is in the pack.
        /// Pressing it puts one in the pack (refused past the slots); the pointer on it shows it in the reading panel.
        /// </summary>
        private ShelfItem MakeItem(SupplyOption option, string kind, TableBox box)
        {
            var item = new ShelfItem { Option = option, Kind = kind };
            item.Button = new DepButton(_root, "ocbg-meta-shelf-item", "border-radius: var(--radius-panel);").Done();
            item.Button.Node.Place(box.X, box.Y, box.W, box.H);

            float sx = (box.W - DepartTable.SocketSize) / 2f;
            RectTransform socket = Ui.Div(item.Button.Root, "socket", sx, 0f, DepartTable.SocketSize, DepartTable.SocketSize);
            UiNode.Of(socket);
            BoxGraphic.On(socket);
            item.Socket = CssStates.Of(socket).Base(TableCss.Socket).State(":hover", TableCss.SocketHover).State(".is-in", TableCss.SocketIn);
            item.Socket.Apply(true);
            string d = DepartTable.LineArtOf(option.Id);
            const float art = 30f;
            PathGraphic g = d != null
                ? PathGraphic.Icon(item.Button.Root, new IconDef(option.Id, d, IconDef.Box24, false), art, DsTokens.Ink)
                : MetaUtil.Icon(item.Button.Root, "plus", art, DsTokens.Ink);
            Ui.Place(g.rectTransform, sx + (DepartTable.SocketSize - art) / 2f, (DepartTable.SocketSize - art) / 2f);

            TextStyle ns = MetaStyle.P(TableCss.ItemName);
            ns.Align = TextAlign.Center;
            ns.LineClamp = (int)DepartTable.ItemNameLines;
            Label.Make(item.Button.Root, option.Name ?? "", ns, 0f, DepartTable.SocketSize + DepartTable.ItemNameGap, box.W, DepartTable.ItemNameLine * DepartTable.ItemNameLines, "name");

            TextStyle cs = MetaStyle.Text(TableCss.ItemCount);
            item.Count = Label.Make(item.Button.Root, "", cs, sx + DepartTable.SocketSize - 4f, -2f, null, null, "count");

            item.Button.Area.HoverChanged += on =>
            {
                if (item.Socket) item.Socket.SetState(":hover", on);
                if (on) _s.Sound.Play("nodeHover");
                _s.HoverItem(on ? option : null, kind);
            };
            item.Button.OnClick = () =>
            {
                DepartureBuilder s = _s.Supplies;
                if (kind == Tool ? s.AddTool(option.Id) : s.AddConsumable(option.Id))
                {
                    _s.Sound.Play("consumableUse");
                    RefreshSupplies();
                }
                else
                {
                    _s.Sound.Play("refusal");
                    _s.Shake(item.Button);
                }
            };
            return item;
        }

        // js: scenes/meta/departure.js:559
        /// <summary>
        /// drawSlots(row, ids, count, options, removeFn, isBonus): `count` plates over the row (DepartTable.Slot) — a full
        /// one names its thing and takes it out on a press (uiCancel), an empty one is a disabled dashed 「空き」; a bonus
        /// slot (the survivor's 4th tool) keeps the wick dashes while empty and says why on hover.
        /// </summary>
        private void DrawSlots(RectTransform row, TableBox rowBox, List<DepButton> plates, IReadOnlyList<string> ids, int count,
            IReadOnlyList<SupplyOption> options, string kind, Func<string, bool> removeFn, Func<int, bool> isBonus)
        {
            Ui.Clear(row);
            plates.Clear();
            for (int i = 0; i < count; i++)
            {
                TableBox box = DepartTable.Slot(rowBox, i, count);
                string id = i < ids.Count ? ids[i] : null;
                SupplyOption opt = null;
                if (id != null) foreach (SupplyOption o in options) if (o.Id == id) { opt = o; break; }
                bool bonus = isBonus != null && isBonus(i);
                bool full = opt != null;
                DepButton plate = null;
                plate = new DepButton(row, "ocbg-meta-slot" + (full ? " is-full" : "") + (bonus ? " is-bonus" : ""), TableCss.Slot)
                    .OnClass("is-full", TableCss.SlotFull)
                    .Rule(".is-full:hover", TableCss.SlotFullHover, () => plate.Has("is-full") && plate.Hovered)
                    .Rule(".is-bonus:not(.is-full)", TableCss.SlotBonus, () => plate.Has("is-bonus") && !plate.Has("is-full"))
                    .OnDisabled(MetaStyle.ButtonDisabledCss);
                if (full) plate.Toggle("is-full", true, apply: false);
                if (bonus) plate.Toggle("is-bonus", true, apply: false);
                plate.Disabled = !full;
                plate.Done();
                plate.Node.Place(box.X, box.Y, box.W, box.H);
                if (bonus) TitleTip.On(plate.Box, DepartureBuilder.BonusLabel);
                string removeId = id;
                plate.OnClick = () =>
                {
                    if (!full || !removeFn(removeId)) return;
                    _s.Sound.Play("uiCancel");
                    _s.HoverItem(null, null);
                    RefreshSupplies();
                };
                if (full) plate.Area.HoverChanged += on => _s.HoverItem(on ? opt : null, kind);

                // the number in its circle (30), then the name or 「空き」
                const float padX = 12f, no = 30f;
                RectTransform n = Ocb.El(plate.Root, "ocbg-meta-slot-n", TableCss.SlotNo);
                Label.Make(n, (i + 1).ToString(CultureInfo.InvariantCulture), MetaStyle.Text(TableCss.SlotNo), "text");
                Ocb.Flow(n, TableCss.SlotNo);
                UiNode.Of(n).Place(padX, (box.H - no) / 2f);
                TextStyle bs = MetaStyle.Text(full ? TableCss.SlotName : TableCss.SlotEmpty);
                string word = full ? opt.Name : MetaText.EmptySlot;
                float tx = padX + no + 10f;
                float avail = Math.Max(1f, box.W - tx - padX);
                Label.Make(plate.Root, word, bs, tx, (box.H - bs.LineHeightPx) / 2f, Math.Min(TextMeasure.Width(word, bs), avail) + 0.01f, bs.LineHeightPx, "b");
                plates.Add(plate);
            }
        }

        // js: scenes/meta/departure.js:638
        /// <summary>
        /// refreshSupplies(): every thing's ring and count, both headings and slot rows, then the screen's status line,
        /// 出立する and the draft.
        /// </summary>
        internal void RefreshSupplies()
        {
            DepartureBuilder supplies = _s.Supplies;
            foreach (ShelfItem item in _items)
            {
                int n = item.Kind == Tool ? supplies.ToolCount(item.Option.Id) : supplies.ConsumableCount(item.Option.Id);
                item.Socket.SetState(".is-in", n > 0);
                Label.SetText(item.Count, n == 0 ? "" : "×" + n.ToString(CultureInfo.InvariantCulture));
            }
            Label.SetText(_toolHead, DepartTable.ToolsHeading(supplies.Tools.Count, supplies.Limits.ToolSlots));
            Label.SetText(_consHead, DepartTable.ConsumablesHeading(supplies.Consumables.Count, supplies.Limits.ConsumableSlots));
            DrawSlots(_toolSlotRow, DepartTable.ToolSlots, _toolSlots, supplies.Tools, supplies.Limits.ToolSlots, supplies.ToolOptions, Tool,
                id => supplies.RemoveTool(id), i => supplies.IsBonusToolSlot(i));
            DrawSlots(_consSlotRow, DepartTable.ConsumableSlots, _consSlots, supplies.Consumables, supplies.Limits.ConsumableSlots, supplies.ConsumableOptions, Consumable,
                id => supplies.RemoveConsumable(id), null);
            _s.RefreshStatus();
            _s.SyncFocus();
        }

        /// <summary>The supplies' buttons in DOM order (the Tab order): the shelf's things, the tool slots, the consumable slots.</summary>
        internal IEnumerable<object> FocusOrder()
        {
            foreach (ShelfItem item in _items) yield return item.Button;
            foreach (DepButton b in _toolSlots) yield return b;
            foreach (DepButton b in _consSlots) yield return b;
        }
    }
}
