// ExploreScene — the carried items (ExploreScene.js:661-741 drawCarried / fitCarried, :1385 useConsumable;
// explore.css:180-210; explore.md §5.6). Owned by EX-PANELS.
//
// The panel (column, gap 12 / trial 10, padding 16 24 / trial 14 24 16) lists the tools (a line, or the trial's chips
// that wrap), the consumable slots (grid of 3) and the rows of what was found and what was handed down. It keeps its
// bottom edge at 960 and grows upward when what it lists needs more room; the node detail above gives up the same
// height. The JS rebuilds it on every render; here it is rebuilt when what it shows changed (the same picture, and a
// hovered slot is not rebuilt under the pointer for nothing).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Exploration;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Explore
{
    public sealed partial class ExploreScene
    {
        // ------------------------------------------------------------------ explore.css, verbatim (the carried items)

        /// <summary>explore.css:181 .ocbg-explore-carried.</summary>
        private const string CarriedCss = "z-index: 6; display: flex; flex-direction: column; gap: 12px; padding: 16px 24px 16px; background: var(--panel); border: 1px solid var(--rule); border-radius: var(--radius-panel);";

        /// <summary>explore.css:193 .ocbg-explore-carried.is-trial.</summary>
        private const string CarriedTrialCss = "gap: 10px; padding-top: 14px;";

        /// <summary>explore.css:182 .ocbg-explore-tools.</summary>
        private const string ToolsCss = "display: flex; align-items: center; gap: 10px; font: 400 20px/26px var(--font-body); white-space: nowrap;";

        /// <summary>explore.css:194 .ocbg-explore-tools.is-chips.</summary>
        private const string ToolsChipsCss = "gap: 6px 8px; flex-wrap: wrap; white-space: normal;";

        /// <summary>explore.css:195 .ocbg-explore-tool.</summary>
        private const string ToolChipCss = "display: inline-flex; align-items: baseline; gap: 6px; padding: 2px 10px; border: 1px solid var(--amber); border-radius: var(--radius-chip-self); background: color-mix(in srgb, var(--amber) 10%, transparent); font: 700 16px/22px var(--font-body); white-space: nowrap; cursor: default;";

        /// <summary>explore.css:196 .ocbg-explore-tool.is-battle.</summary>
        private const string ToolChipBattleCss = "border-color: var(--steel); background: none;";

        /// <summary>explore.css:197 .ocbg-explore-tool-effect.</summary>
        private const string ToolEffectCss = "font-weight: 400; font-size: 17px; color: var(--ink);";

        /// <summary>explore.css:183 .ocbg-explore-slots.</summary>
        private const string SlotsCss = "display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px;";

        /// <summary>explore.css:184 .ocbg-explore-slot.</summary>
        private const string SlotCss = "position: relative; height: 112px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; padding: 8px; color: var(--ink); background: var(--panel-solid); border: 2px solid var(--steel); border-radius: var(--radius-panel); font: 700 18px/22px var(--font-body); cursor: default; transition: transform 90ms, border-color 120ms, background-color 120ms;";

        /// <summary>explore.css:185 .ocbg-explore-slot.is-usable.</summary>
        private const string SlotUsableCss = "border-color: var(--amber); cursor: pointer;";

        /// <summary>explore.css:186 .ocbg-explore-slot.is-usable:hover.</summary>
        private const string SlotUsableHoverCss = "transform: translateY(-4px); background: color-mix(in srgb, var(--amber) 12%, var(--panel-solid));";

        /// <summary>explore.css:187 .ocbg-explore-slot.is-empty.</summary>
        private const string SlotEmptyCss = "border-style: dashed; font-weight: 400;";

        /// <summary>explore.css:188 .ocbg-explore-slot[disabled]:not(.is-empty).</summary>
        private const string SlotDisabledCss = "opacity: .55;";

        /// <summary>explore.css:189 .ocbg-explore-slot kbd (over the scene's kbd, explore.css:11).</summary>
        private const string SlotKbdCss = "position: absolute; left: 8px; top: 8px; height: 22px; min-width: 22px; font-size: 16px;";

        /// <summary>explore.css:190 .ocbg-explore-slot.is-empty kbd.</summary>
        private const string SlotEmptyKbdCss = "opacity: .5;";

        /// <summary>explore.css:191 .ocbg-explore-slot-use.</summary>
        private const string SlotUseCss = "position: absolute; right: 10px; top: 8px; font: 400 16px/22px var(--font-body);";

        /// <summary>explore.css:198 .is-trial .ocbg-explore-slot.</summary>
        private const string SlotTrialCss = "height: auto; min-height: 120px; gap: 2px; padding-top: 12px;";

        /// <summary>explore.css:199 .ocbg-explore-slot-fc.</summary>
        private const string SlotFcCss = "display: flex; flex-direction: column; align-items: center; max-width: 100%; font: 400 17px/22px var(--font-body); color: var(--ink); text-align: center;";

        /// <summary>explore.css:200 .ocbg-explore-slot-fcl.</summary>
        private const string SlotFclCss = "white-space: nowrap;";

        /// <summary>explore.css:201 .ocbg-explore-slot-fc.is-reason.</summary>
        private const string SlotReasonCss = "font-size: 16px; white-space: normal; text-align: center;";

        /// <summary>explore.css:202 .ocbg-explore-carried.is-held .ocbg-explore-slot.</summary>
        private const string SlotHeldCss = "pointer-events: none; opacity: .5; border-color: var(--steel);";

        /// <summary>explore.css:203 .ocbg-explore-carried.is-held .ocbg-explore-slot-use.</summary>
        private const string SlotHeldUseCss = "visibility: hidden;";

        /// <summary>explore.css:204 .ocbg-explore-slot.is-aiming (its animation runs through CssAnimator: SlotAimingAnim).</summary>
        private const string SlotAimingCss = "border-color: var(--wick); box-shadow: var(--shadow-lamp); animation: ocbg-explore-pulse 1.6s ease-in-out infinite; cursor: pointer;";

        private const string SlotAimingAnim = "ocbg-explore-pulse 1.6s ease-in-out infinite";

        /// <summary>explore.css:209 .ocbg-explore-found.</summary>
        private const string FoundCss = "display: flex; flex-wrap: wrap; align-items: center; gap: 8px; font: 400 16px/22px var(--font-body);";

        /// <summary>explore.css:210 .ocbg-explore-chip.</summary>
        private const string ChipCss = "display: inline-flex; align-items: center; gap: 6px; padding: 1px 10px; border: 1px solid var(--rule); border-radius: var(--radius-chip-self); white-space: nowrap;";

        /// <summary>The 12 px between the node detail and the carried panel (ExploreScene.js:739: DETAIL.h = top − 12 − DETAIL.y).</summary>
        private const float CarriedDetailGap = 12f;

        // ------------------------------------------------------------------ the panel's state

        /// <summary>What the panel shows (it is rebuilt only when this changes).</summary>
        private string CarriedKey;

        /// <summary>The panel's height when its content is laid out (offsetHeight with height: auto).</summary>
        private float CarriedNeedH = ExploreStyle.CarriedH;

        /// <summary>The slots built last (SyncHeld restyles them).</summary>
        private readonly List<ExSlotView> CarriedSlots = new List<ExSlotView>();

        /// <summary>The slots the panel added to the Tab scope (dropped when it is rebuilt).</summary>
        private readonly List<Focusable> CarriedFocus = new List<Focusable>();

        /// <summary>
        /// Styles the carried panel (.ocbg-explore-carried: panel, 1px rule, radius 6, column gap 12, padding 16 24). The
        /// core made Carried at CARRIED with z 6. Called once from Mount.
        /// </summary>
        // js: scenes/explore/ExploreScene.js:274
        private void BuildCarried()
        {
            BoxGraphic box = BoxGraphic.On(Carried, CarriedCss);
            box.raycastTarget = true; // a div takes the pointer
        }

        // js: scenes/explore/ExploreScene.js:662
        /// <summary>drawCarried(): the tools (or the trial's tool chips), the three slots, the found and inherited rows; then FitCarried.</summary>
        private void DrawCarried()
        {
            if (Screen == null || !Carried) return;
            bool trial = Session.Trial;
            string key = CarriedKeyOf(trial);
            if (key == CarriedKey)
            {
                // The JS rebuilds the panel here: a slot Tab had focused goes with its element (the focus falls back to
                // the body), so Enter acts on the map again rather than on that slot.
                Focusable cur = Focus.Current;
                if (cur != null && CarriedFocus.Contains(cur)) Focus.Blur();
                SyncHeld();
                return;
            }
            CarriedKey = key;
            DropPanelFocus(CarriedFocus);
            Ui.Clear(Carried);
            CarriedSlots.Clear();

            // carried.classList.toggle('is-trial', trial)
            BoxStyle panel = global::Trial.Geometry.Css.Box(CarriedCss + (trial ? CarriedTrialCss : ""));
            float gap = trial ? 10f : 12f;
            float x0 = 1f + panel.Padding.Left;
            float cw = ExploreStyle.CarriedW - 2f - panel.Padding.Horizontal;
            float y = 1f + panel.Padding.Top;

            // ---- the tools ----
            if (trial && Screen.ToolCards.Count > 0) y += CarriedToolChips(x0, y, cw);
            else y += CarriedToolsLine(x0, y);

            // ---- the slots ----
            y += gap;
            y += CarriedSlotGrid(x0, y, cw, trial);

            // ---- found this life / handed down (INHERITANCE.md R-27: the pages handed down get their own row) ----
            // trial: the found consumables are in the slots (or were swapped away), so only the pages are listed
            IReadOnlyList<string> found = trial ? (IReadOnlyList<string>)Array.Empty<string>() : Expedition.Consumables;
            int inherited = Math.Max(0, Math.Min(Expedition.Pages, Expedition.InheritedPages));
            int pages = Expedition.Pages - inherited;
            if (found.Count > 0 || pages > 0)
            {
                // title: trial ? T.FoundNoteTrial : T.FoundNote (the browser's tooltip; not drawn here)
                var chips = new List<(string Icon, string Text, string Count)>();
                var counts = new List<KeyValuePair<string, int>>();
                foreach (string id in found)
                {
                    int at = counts.FindIndex(kv => kv.Key == id);
                    if (at >= 0) counts[at] = new KeyValuePair<string, int>(id, counts[at].Value + 1);
                    else counts.Add(new KeyValuePair<string, int>(id, 1));
                }
                foreach (KeyValuePair<string, int> kv in counts)
                    chips.Add((ExploreIcons.ItemIcon(kv.Key), ExplorationText.ItemName(kv.Key), kv.Value > 1 ? "×" + kv.Value.ToString(CultureInfo.InvariantCulture) : null));
                if (pages > 0) chips.Add(("page", null, ExploreText.Pages(pages)));
                y += gap;
                y += CarriedFoundRow(x0, y, cw, ExploreText.Found, chips, "ocbg-explore-found");
            }
            if (inherited > 0)
            {
                // title: T.InheritedNote; the chip's title: T.PagesNote
                y += gap;
                y += CarriedFoundRow(x0, y, cw, ExploreText.Inherited,
                    new List<(string Icon, string Text, string Count)> { ("page", null, ExploreText.Pages(inherited)) }, "ocbg-explore-found is-inherited");
            }

            CarriedNeedH = y + panel.Padding.Bottom + 1f;
            SyncHeld();
            FitCarried();
        }

        /// <summary>Everything the panel draws, as one string: the panel is rebuilt when it changes.</summary>
        private string CarriedKeyOf(bool trial)
        {
            const char sep = '\u0001';
            var sb = new StringBuilder(256);
            sb.Append(trial ? '1' : '0').Append(sep).Append(Screen.ToolsLabel).Append(sep);
            foreach (ToolCard t in Screen.ToolCards) sb.Append(t.Name).Append(sep).Append(t.Side).Append(sep).Append(t.Effect).Append(sep).Append(t.Note).Append(sep);
            sb.Append('|');
            foreach (SlotCard s in Screen.Consumables)
            {
                sb.Append(s.Index).Append(sep).Append(s.Name).Append(sep).Append(s.Note).Append(sep).Append(s.CanUse ? '1' : '0').Append(s.Aiming ? '1' : '0').Append(sep);
                sb.Append(s.Forecast != null ? "F" + string.Join("\u0002", s.Forecast) : "-").Append(sep).Append(s.Reason).Append(sep).Append(SlotItemId(s)).Append(sep);
            }
            sb.Append('|');
            if (!trial) foreach (string id in Expedition.Consumables) sb.Append(id).Append(sep);
            sb.Append('|').Append(Expedition.Pages).Append(sep).Append(Expedition.InheritedPages);
            return sb.ToString();
        }

        /// <summary>
        /// The C# screen's tools line (flex, align-items: center, nowrap): tool 22 + ToolsLabel (400 20/26; its line box
        /// taller when it carries numbers), each centred in the row. Returns its height.
        /// </summary>
        private float CarriedToolsLine(float x0, float y)
        {
            TextStyle ts = PanelText(ToolsCss, true);
            string label = Screen.ToolsLabel ?? "";
            float lh = PanelLineOf(ts, HasDigits(label)).H;
            float rowH = Math.Max(22f, lh);
            PanelIcon(Carried, "tool", 22f, ExploreStyle.Ink, x0, y + (rowH - 22f) / 2f);
            string text = Rich(label, ts);
            Label.Make(Carried, text, ts, x0 + 22f + 10f, y + (rowH - lh) / 2f, TextMeasure.Width(text, ts), lh, "ocbg-explore-tools");
            return rowH;
        }

        /// <summary>
        /// Trial: each tool as a chip with what it does (tools_and_consumables_v4 §2), wrapping (gap 6 8); its note in the
        /// hint while the pointer is on it. Returns the rows' height.
        /// </summary>
        private float CarriedToolChips(float x0, float y, float cw)
        {
            RectTransform tools = Ui.Div(Carried, "ocbg-explore-tools is-chips", x0, y, cw, 0f);
            var items = new List<PanelItem>();
            PathGraphic icon = PanelIcon(tools, "tool", 22f, ExploreStyle.Ink, 0f, 0f);
            items.Add(new PanelItem(icon.rectTransform, 22f, 22f));
            TextStyle ns = PanelText(ToolChipCss);
            TextStyle es = PanelText(ToolChipCss + ToolEffectCss, true);
            // align-items: baseline between the name (16/22) and the effect (17/22)
            float bn = TextMath.CssBaselineFromTop(ns.Spec), be = TextMath.CssBaselineFromTop(es.Spec), mb = Math.Max(bn, be);
            float nTop = mb - bn, eTop = mb - be;
            foreach (ToolCard t in Screen.ToolCards)
            {
                bool battle = t.Side == ExplorationText.SideBattle;
                string effectText = string.IsNullOrEmpty(t.Effect) ? t.Side : t.Effect;
                string effect = Rich(effectText, es);
                // the effect's numbers (濃さ −1) make its line box 23: the chip is 29 tall (the reference shots)
                float eh = PanelLineOf(es, HasDigits(effectText)).H;
                float contentH = Math.Max(nTop + ns.LineHeightPx, eTop + eh);
                float nw = TextMeasure.Width(t.Name, ns), ew = TextMeasure.Width(effect, es);
                float w = 1f + 10f + nw + 6f + ew + 10f + 1f, h = 1f + 2f + contentH + 2f + 1f;
                BoxGraphic chip = BoxGraphic.Make(tools, ToolChipCss + (battle ? ToolChipBattleCss : ""), 0f, 0f, w, h, battle ? "ocbg-explore-tool is-battle" : "ocbg-explore-tool");
                Label.Make(chip.rectTransform, t.Name, ns, 1f + 10f, 1f + 2f + nTop, nw, ns.LineHeightPx, "ocbg-explore-tool-name");
                Label.Make(chip.rectTransform, effect, es, 1f + 10f + nw + 6f, 1f + 2f + eTop, ew, eh, "ocbg-explore-tool-effect");
                string note = ExploreText.ToolNote(t.Name, t.Side, t.Note);
                ClickArea area = ClickArea.On(chip);
                area.Enter += _ =>
                {
                    HintOverride = note;
                    DrawHint();
                };
                area.Exit += _ =>
                {
                    HintOverride = null;
                    DrawHint();
                };
                items.Add(new PanelItem(chip.rectTransform, w, h));
            }
            float rowsH = PanelWrap(items, cw, 8f, 6f);
            Ui.Size(tools, cw, rowsH);
            return rowsH;
        }

        /// <summary>The id whose icon a slot shows: slot.Id, or the loadout's item in that slot; null when empty.</summary>
        private string SlotItemId(SlotCard slot)
        {
            if (slot.Name == ExplorationText.EmptySlot) return null;
            if (!string.IsNullOrEmpty(slot.Id)) return slot.Id;
            var list = Session.State.Loadout.Consumables;
            return slot.Index >= 0 && slot.Index < list.Count ? list[slot.Index]?.Id : null;
        }

        /// <summary>What one slot holds and how tall its content is (measured before the grid's row is known).</summary>
        private sealed class ExSlotPlan
        {
            public SlotCard Slot;
            public bool Empty;
            public string Id;
            /// <summary>trial: what using it changes (one line per change) or why not; "" otherwise.</summary>
            public string Extra;
            public float IconSize;
            public float NameH;
            public float ExtraH;
            /// <summary>The forecast's lines' line boxes (23 with their numbers at 17px/22px).</summary>
            public float[] FcLineH;
            /// <summary>The reason's style to draw with (its line pitch) and the y shift that keeps its first baseline.</summary>
            public TextStyle ReasonStyle;
            public float ReasonShift;
            public float Gap;
            public float PadTop;
            public float ContentH;
            public float NaturalH;
        }

        /// <summary>A slot as built: its button, its 「使う」/「やめる」, and whether the panel holds it.</summary>
        private sealed class ExSlotView
        {
            public ExPanelButton Button;
            public GameObject Use;
            public bool Held;
            public bool Applied;

            /// <summary>
            /// .is-held on the panel: no pointer, opacity .5, steel border, the use label hidden. instant: a slot built
            /// under the held panel starts held (a new element never transitions from the look it did not have).
            /// </summary>
            public void SetHeld(bool held, bool instant = false)
            {
                if (Button == null || !Button.Root) return;
                if (Applied && Held == held) return;
                Held = held;
                Applied = true;
                Button.Node.PointerEvents = held ? PointerEvents.None : PointerEvents.Inherit;
                if (Use) Use.SetActive(!held);
                Button.Restate(instant);
            }
        }

        /// <summary>The slots (grid of 3, gap 12; a trial row as tall as its tallest slot, min 120). Returns the grid's height.</summary>
        private float CarriedSlotGrid(float x0, float y, float cw, bool trial)
        {
            IReadOnlyList<SlotCard> slots = Screen.Consumables;
            float slotW = (cw - 2f * 12f) / 3f;
            float inner = slotW - 2f * 2f - 2f * 8f;
            var plans = new List<ExSlotPlan>(slots.Count);
            foreach (SlotCard slot in slots) plans.Add(SlotPlan(slot, trial, inner));
            float gy = 0f;
            for (int row = 0; row * 3 < plans.Count; row++)
            {
                float rowH = 0f;
                for (int c = 0; c < 3 && row * 3 + c < plans.Count; c++)
                    rowH = Math.Max(rowH, trial ? Math.Max(120f, plans[row * 3 + c].NaturalH) : 112f);
                if (row > 0) gy += 12f;
                for (int c = 0; c < 3 && row * 3 + c < plans.Count; c++)
                    BuildSlot(plans[row * 3 + c], trial, x0 + c * (slotW + 12f), y + gy, slotW, rowH, inner);
                gy += rowH;
            }
            return gy;
        }

        private ExSlotPlan SlotPlan(SlotCard slot, bool trial, float inner)
        {
            var p = new ExSlotPlan { Slot = slot, Empty = slot.Name == ExplorationText.EmptySlot };
            p.Id = SlotItemId(slot);
            // trial: what using it changes (or why not), under its name — one line per change, so a forecast of two
            // changes (浄化の香 across a 20% line) wraps inside its slot instead of widening the row
            p.Extra = trial && !p.Empty ? (slot.Forecast != null ? string.Join("　", slot.Forecast) : slot.Reason ?? "") : "";
            p.IconSize = p.Empty ? 0f : trial ? 34f : 40f;
            p.Gap = trial ? 2f : 6f;
            p.PadTop = trial ? 12f : 8f;
            p.NameH = TextMeasure.Height(slot.Name ?? "", SlotNameStyle(p.Empty), inner);
            if (p.Extra.Length > 0)
            {
                if (slot.Forecast != null)
                {
                    // one nowrap line per change (flex column): each its own line box
                    TextStyle fs = PanelText(SlotFcCss + SlotFclCss, true, TextAlign.Center);
                    p.FcLineH = new float[slot.Forecast.Count];
                    for (int i = 0; i < slot.Forecast.Count; i++)
                    {
                        p.FcLineH[i] = PanelLineOf(fs, HasDigits(slot.Forecast[i])).H;
                        p.ExtraH += p.FcLineH[i];
                    }
                }
                else
                {
                    TextStyle rs = PanelText(SlotFcCss + SlotReasonCss, true, TextAlign.Center);
                    p.ExtraH = PanelRichBlock(Rich(p.Extra, rs), rs, inner, HasDigits(p.Extra), out p.ReasonStyle, out p.ReasonShift);
                }
            }
            int n = 0;
            float h = 0f;
            if (!p.Empty) { h += p.IconSize; n++; }
            h += p.NameH;
            n++;
            if (p.Extra.Length > 0) { h += p.ExtraH; n++; }
            p.ContentH = h + p.Gap * (n - 1);
            p.NaturalH = 2f + p.PadTop + p.ContentH + 8f + 2f;
            return p;
        }

        /// <summary>The slot name's style: 700 18/22 (400 when empty), centred as a button's content is.</summary>
        private TextStyle SlotNameStyle(bool empty) => PanelText(SlotCss + (empty ? SlotEmptyCss : ""), false, TextAlign.Center);

        /// <summary>
        /// One slot (ExploreScene.js:683-701): a button with its key, its item's icon, its name, what using it would do (or
        /// why not), and 「使う」/「やめる」 when it can be pressed; its note in the hint while the pointer is on it.
        /// </summary>
        private void BuildSlot(ExSlotPlan p, bool trial, float x, float y, float w, float h, float inner)
        {
            SlotCard slot = p.Slot;
            bool usable = slot.CanUse, aiming = slot.Aiming;
            bool disabled = !(usable || aiming);
            ExPanelButton b = ExPanelButton.Make(Carried, "ocbg-explore-slot", PanelBoxCss(SlotCss), x, y, w, h);
            b.Text = slot.Name ?? "";
            // the rules in cascade order (specificity, then explore.css order)
            b.States.State(":focus-visible", PanelFocusCss)
                .State(".is-usable", SlotUsableCss, usable)
                .State(".is-empty", SlotEmptyCss, p.Empty)
                .State(".is-trial", PanelBoxCss(SlotTrialCss), trial)
                .State(".is-aiming", SlotAimingCss, aiming)
                .State(".is-usable:hover", SlotUsableHoverCss)
                .State("[disabled]:not(.is-empty)", SlotDisabledCss, disabled && !p.Empty)
                .State(".is-held", SlotHeldCss);
            var view = new ExSlotView { Button = b };
            b.Restyle = s =>
            {
                s.States.SetState(":focus-visible", s.FocusRing, false);
                s.States.SetState(".is-held", view.Held, false);
                s.States.SetState(".is-usable:hover", usable && s.Hovered && !view.Held, false);
            };
            b.Disabled = disabled;

            // kbd 1–3 (absolute, left 8 top 8 of the padding box)
            TextStyle ks = PanelText(ExploreStyle.KbdCss + SlotKbdCss, false, TextAlign.Center);
            string key = (slot.Index + 1).ToString(CultureInfo.InvariantCulture);
            float kw = Math.Max(22f, TextMeasure.Width(key, ks) + 6f + 6f + 2f);
            BoxGraphic kbd = BoxGraphic.Make(b.Root, ExploreStyle.KbdCss + SlotKbdCss, 2f + 8f, 2f + 8f, kw, 22f, "kbd");
            Label.Make(kbd.rectTransform, key, ks, 1f, 1f + (20f - ks.LineHeightPx) / 2f, kw - 2f, ks.LineHeightPx, "key");
            if (p.Empty) UiNode.Of(kbd.rectTransform).Opacity = 0.5f; // .is-empty kbd { opacity: .5 }

            // the content, centred in the content box (justify-content: center)
            float cx = 2f + 8f;
            float cy = 2f + p.PadTop + (h - 2f - p.PadTop - 8f - 2f - p.ContentH) / 2f;
            if (!p.Empty)
            {
                PanelIcon(b.Root, ExploreIcons.ItemIcon(p.Id), p.IconSize, ExploreStyle.Ink, cx + (inner - p.IconSize) / 2f, cy);
                cy += p.IconSize + p.Gap;
            }
            Label.Make(b.Root, slot.Name ?? "", SlotNameStyle(p.Empty), cx, cy, inner, p.NameH, "ocbg-explore-slot-name");
            cy += p.NameH;
            if (p.Extra.Length > 0)
            {
                cy += p.Gap;
                if (slot.Forecast != null)
                {
                    TextStyle fs = PanelText(SlotFcCss + SlotFclCss, true, TextAlign.Center);
                    for (int i = 0; i < slot.Forecast.Count; i++)
                    {
                        Label.Make(b.Root, Rich(slot.Forecast[i], fs), fs, cx, cy, inner, p.FcLineH[i], "ocbg-explore-slot-fcl");
                        cy += p.FcLineH[i];
                    }
                }
                else
                {
                    TextStyle rs = PanelText(SlotFcCss + SlotReasonCss, true, TextAlign.Center);
                    Label.Make(b.Root, Rich(p.Extra, rs), p.ReasonStyle ?? rs, cx, cy + p.ReasonShift, inner, p.ExtraH, "ocbg-explore-slot-fc is-reason");
                }
            }

            // 「使う」 / 「やめる」 (absolute, right 10 top 8)
            if (usable || aiming)
            {
                TextStyle us = PanelText(SlotUseCss);
                string use = aiming ? ExploreText.StopAim : ExploreText.Use;
                float uw = TextMeasure.Width(use, us);
                view.Use = Label.Make(b.Root, use, us, w - 2f - 10f - uw, 2f + 8f, uw, us.LineHeightPx, "ocbg-explore-slot-use").gameObject;
            }

            // listeners (ExploreScene.js:698-700)
            string extra = p.Extra;
            b.Area.Enter += _ =>
            {
                HintOverride = !string.IsNullOrEmpty(slot.Note) ? ExploreText.SlotHint(slot.Note, extra) : null;
                DrawHint();
            };
            b.Area.Exit += _ =>
            {
                HintOverride = null;
                DrawHint();
            };
            int index = slot.Index;
            b.Click += () => UseConsumable(index);

            b.Restate(instant: true);
            if (aiming) CssAnimator.Play(b.Root, SlotAimingAnim);
            view.SetHeld(Plate != null && Plate.Kind != ExploreStyle.PlateScreen, instant: true);
            CarriedSlots.Add(view);
            Focusable f = AddFocus(b.Root, b.Press, b.SetFocusRing, () => !b.Disabled, () => b.Text);
            if (f != null) CarriedFocus.Add(f);
        }

        /// <summary>
        /// A row of what the life holds (.ocbg-explore-found: wrap, gap 8): the key, then chips (icon 18, the name, ×n).
        /// Each chip is (icon, plain text, rich text). Returns the rows' height.
        /// </summary>
        private float CarriedFoundRow(float x0, float y, float cw, string key, List<(string Icon, string Text, string Count)> chips, string name)
        {
            RectTransform row = Ui.Div(Carried, name, x0, y, cw, 0f);
            var items = new List<PanelItem>();
            TextStyle ks = PanelText(KeyCss);
            float kw = TextMeasure.Width(key, ks);
            TMP_Text k = Label.Make(row, key, ks, 0f, 0f, kw, ks.LineHeightPx, "ocbg-explore-k");
            items.Add(new PanelItem(k.rectTransform, kw, ks.LineHeightPx));
            TextStyle plain = PanelText(FoundCss);
            TextStyle rich = PanelText(FoundCss, true);
            foreach ((string icon, string text, string count) in chips)
            {
                string countM = count != null ? Rich(count, rich) : null;
                float tw = text != null ? TextMeasure.Width(text, plain) : 0f;
                float nw = countM != null ? TextMeasure.Width(countM, rich) : 0f;
                float inner = 18f + (text != null ? 6f + tw : 0f) + (countM != null ? 6f + nw : 0f);
                // inline-flex, align-items: center: the icon (18), the name (a 22 px line), the rich count (希少な頁 3 枚,
                // ×2: its numbers make its line box 23), each centred in the chip's content box
                float th = text != null ? plain.LineHeightPx : 0f;
                float ch = countM != null ? PanelLineOf(rich, HasDigits(count)).H : 0f;
                float ih = Math.Max(18f, Math.Max(th, ch));
                float w = 1f + 10f + inner + 10f + 1f, h = 1f + 1f + ih + 1f + 1f;
                BoxGraphic chip = BoxGraphic.Make(row, ChipCss, 0f, 0f, w, h, "ocbg-explore-chip");
                float cx = 1f + 10f, top = 2f;
                PanelIcon(chip.rectTransform, icon, 18f, ExploreStyle.Ink, cx, top + (ih - 18f) / 2f);
                cx += 18f;
                if (text != null)
                {
                    cx += 6f;
                    Label.Make(chip.rectTransform, text, plain, cx, top + (ih - th) / 2f, tw, th, "name");
                    cx += tw;
                }
                if (countM != null)
                {
                    cx += 6f;
                    Label.Make(chip.rectTransform, countM, rich, cx, top + (ih - ch) / 2f, nw, ch, "count");
                }
                items.Add(new PanelItem(chip.rectTransform, w, h));
            }
            float rowsH = PanelWrap(items, cw, 8f, 8f);
            Ui.Size(row, cw, rowsH);
            return rowsH;
        }

        // js: scenes/explore/ExploreScene.js:733
        /// <summary>
        /// The carried panel keeps its bottom edge and grows upward when what it lists needs more room (the survivor's 4th
        /// tool wraps the chips onto a second row: every tool stays listed, W-2 / L-7); the node detail above gives up the
        /// same height (DETAIL.h = top − 12 − 112; DetailKey = "" when it changed) and sets itself tighter if it must.
        /// </summary>
        private void FitCarried()
        {
            if (!Carried || !Detail) return;
            // offsetHeight is a whole number of px
            float need = Math.Max(ExploreStyle.CarriedH, (float)Math.Ceiling(Math.Round(CarriedNeedH, MidpointRounding.AwayFromZero)));
            float top = ExploreStyle.CarriedY + ExploreStyle.CarriedH - need;
            UiNode.Of(Carried).Place(ExploreStyle.CarriedX, top, ExploreStyle.CarriedW, need);
            float dh = top - CarriedDetailGap - ExploreStyle.DetailY;
            UiNode detail = UiNode.Of(Detail);
            if (Math.Abs(detail.H - dh) > 0.001f)
            {
                detail.H = dh;
                DetailKey = "";
            }
        }

        /// <summary>
        /// carried.classList.toggle('is-held', kind !== 'screen') (openPlate) / remove (closePlate): the slots wait under a
        /// card that waits on the player (W-8). Called from SyncMarks, which openPlate and closePlate call at that moment.
        /// </summary>
        private void SyncHeld()
        {
            bool held = Plate != null && Plate.Kind != ExploreStyle.PlateScreen;
            foreach (ExSlotView v in CarriedSlots) v.SetHeld(held);
        }

        // js: scenes/explore/ExploreScene.js:1385
        /// <summary>
        /// A slot pressed (mouse or 1–3). The same rule for the mouse as for the keys: only on the map, the layer-break
        /// card and the interlude (W-8) — never under a result, trace, training or choice card, nor while a step or a
        /// confirmation runs.
        /// </summary>
        private void UseConsumable(int index)
        {
            if (Busy || ConfirmOpen || (Plate != null && Plate.Kind != ExploreStyle.PlateScreen)) return;
            if (!Session.CanUseConsumables) return;
            bool aiming = Session.IsAiming;
            if (!Session.UseConsumable(index))
            {
                // a refused call changes nothing; redraw anyway if the aim moved, so the map never shows a stale aim
                if (Session.IsAiming != aiming) Render();
                return;
            }
            // trial: a 標の石 starts (or stops) aiming instead of being spent
            if (Session.IsAiming || aiming) Sfx(Session.IsAiming ? "uiClick" : "uiCancel");
            else Sfx("consumableUse");
            HintOverride = null;
            Persist();
            Render();
        }
    }
}
