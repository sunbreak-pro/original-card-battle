// ExploreScene — the band (ExploreScene.js:285-323 its DOM, :751-791 drawBand, :889-906 canTrainNow / syncMarks /
// syncEndLife, :2270-2278 its buttons' listeners; explore.css:25-65; explore.md §5.3). Owned by EX-PANELS.
//
// The band is built once (BuildBand) and restyled on every render (DrawBand), as the JS keeps its elements. Its flex
// row (gap 24, padding 0 24, items centred in the 95 px content box) is laid out here in stage px: the layer 24…384,
// the collapse calendar 408…1022 (where the JS had 刻限 and the 瘴気 gauge), the body 1046…1506, the three buttons
// right-aligned to 1896.
//
// The calendar (dungeon_exploration_v4.md §6.2 / §6.4, #888) is a Unity design of its own, provisional until design
// draws it: 「残り N 日」 in the centre, and under it the coming month as pips, the day the next new node takes drawn
// faint. While a king is down the calendar stands still: the label dims, and the bridge's note (「竜王が戻るまで 41 日。
// 暦は止まっている」) takes the pips' row. Every number and word is the bridge's (ExplorationScreen).
//
// This file also holds the helpers the three panel files share (the band, the node detail, the carried items):
// ExPanelButton (a <button> that is not an .ocbg-btn), the text-style cache, icons and the flex-wrap rows.
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
        // ------------------------------------------------------------------ explore.css, verbatim (the band)

        /// <summary>explore.css:26 .ocbg-explore-band.</summary>
        private const string BandCss = "position: absolute; left: 0; top: 0; width: 1920px; height: 96px; z-index: 7; display: flex; align-items: center; gap: 24px; padding: 0 24px; background: var(--panel); border-bottom: 1px solid var(--rule); box-shadow: 0 6px 18px color-mix(in srgb, var(--ground) 60%, transparent);";

        /// <summary>explore.css:27 .ocbg-explore-row.</summary>
        private const string BandRowCss = "display: flex; align-items: center; gap: 8px; white-space: nowrap;";

        /// <summary>explore.css:28 .ocbg-explore-band-layer.</summary>
        private const string BandLayerCss = "width: 360px; display: flex; align-items: center; gap: 12px; flex: none;";

        /// <summary>explore.css:29 .ocbg-explore-layer-title (its digits: explore.css:30, Rich(numsInFace)).</summary>
        private const string LayerTitleCss = "font: 700 28px/36px var(--font-display); letter-spacing: .04em; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;";

        /// <summary>The calendar's block: the JS's 刻限 (210) and 瘴気 (380) blocks and the gap between them.</summary>
        private const string BandCalendarCss = "width: 614px; flex: none;";

        /// <summary>explore.css:32 .ocbg-explore-band-label.</summary>
        private const string BandLabelCss = "font: 700 22px/28px var(--font-body); white-space: nowrap;";

        /// <summary>explore.css:36 .ocbg-explore-pips i.</summary>
        private const string PipCss = "width: 14px; height: 14px; border-radius: 3px; border: 2px solid var(--steel); transition: background-color 420ms ease-out, border-color 420ms ease-out, box-shadow 420ms ease-out;";

        /// <summary>explore.css:37 .ocbg-explore-pips i.is-left: a day still to come.</summary>
        private const string PipLeftCss = "background: var(--amber); border-color: var(--amber); box-shadow: 0 0 8px color-mix(in srgb, var(--amber) 45%, transparent);";

        /// <summary>explore.css:64 .ocbg-explore-pips.is-long i (the calendar's pips are always the long row's).</summary>
        private const string PipLongCss = "width: 12px; height: 12px;";

        /// <summary>The day the next new node takes (§6.4: drawn faint ahead of time). Unity's own rule.</summary>
        private const string PipNextCss = "background: color-mix(in srgb, var(--amber) 30%, transparent); border-color: color-mix(in srgb, var(--amber) 60%, transparent); box-shadow: none;";

        /// <summary>A day left while a king is down and the calendar stands still. Unity's own rule.</summary>
        private const string PipFrozenCss = "background: var(--steel); border-color: var(--steel); box-shadow: none;";

        /// <summary>The note of a calendar that stands still (the JS forecast's face, explore.css:40).</summary>
        private const string CalendarNoteCss = "font: 400 18px/24px var(--font-body); white-space: nowrap;";

        /// <summary>explore.css:47 .ocbg-explore-band-body.</summary>
        private const string BandBodyCss = "width: 460px; flex: none; display: grid; grid-template-columns: 186px 1fr; grid-template-rows: 30px 26px; column-gap: 16px; row-gap: 6px; align-items: center;";

        /// <summary>explore.css:48 .ocbg-explore-small.</summary>
        private const string BandSmallCss = "font: 400 16px/22px var(--font-body); white-space: nowrap;";

        /// <summary>explore.css:49 .ocbg-explore-marks.</summary>
        private const string MarksCss = "justify-self: start; font: 400 16px/22px var(--font-body); color: var(--ink); background: none; border: 1px solid transparent; border-radius: var(--radius-chip-self); padding: 0 10px; margin-left: -10px; white-space: nowrap; cursor: default;";

        /// <summary>explore.css:50 .ocbg-explore-marks:not([disabled]).</summary>
        private const string MarksEnabledCss = "border-color: var(--amber); cursor: pointer; background: color-mix(in srgb, var(--amber) 12%, transparent);";

        /// <summary>explore.css:51 .ocbg-explore-marks:not([disabled]):hover.</summary>
        private const string MarksHoverCss = "background: color-mix(in srgb, var(--amber) 24%, transparent);";

        /// <summary>explore.css:52 .ocbg-explore-band-buttons.</summary>
        private const string BandButtonsCss = "margin-left: auto; display: flex; gap: 8px;";

        /// <summary>explore.css:53 .ocbg-explore-bandbtn.</summary>
        private const string BandBtnCss = "height: 72px; padding: 6px 12px 4px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px; color: var(--ink); background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel); cursor: pointer; font: 400 16px/20px var(--font-body); white-space: nowrap; transition: border-color 90ms, background-color 90ms, transform 90ms;";

        /// <summary>explore.css:54 .ocbg-explore-bandbtn:hover:not([disabled]).</summary>
        private const string BandBtnHoverCss = "border-color: var(--ink); transform: translateY(-2px);";

        /// <summary>explore.css:55 .ocbg-explore-bandbtn[disabled].</summary>
        private const string BandBtnDisabledCss = "opacity: .4; cursor: default;";

        /// <summary>explore.css:57 .ocbg-explore-bandbtn / -slot / -marks :focus-visible.</summary>
        private const string PanelFocusCss = "outline: 2px solid var(--wick); outline-offset: 2px;";

        // ------------------------------------------------------------------ the band's layout (from the CSS above)

        /// <summary>The band's content box: 96 − the 1 px bottom border (border-box); items are centred in it.</summary>
        private const float BandInnerH = ExploreStyle.BandH - 1f;

        private const float BandPadX = 24f, BandGap = 24f;
        private const float BandLayerX = BandPadX, BandLayerW = 360f;
        private const float BandCalX = BandLayerX + BandLayerW + BandGap, BandCalW = 614f;
        private const float BandBodyX = BandCalX + BandCalW + BandGap, BandBodyW = 460f;

        /// <summary>The coming days the calendar's pips show: a month.</summary>
        private const int CalendarPips = 30;

        private const float CalPipSize = 12f, CalPipGap = 4f;

        /// <summary>The second row (the pips, or the note of a calendar that stands still): its top under the row and its height.</summary>
        private const float BandCalRow2Gap = 10f, BandCalRow2H = 24f;

        /// <summary>
        /// The calendar row's height: the label's line box (22px/28px with its numbers in the mono face: 30, PanelLineOf),
        /// over the 22 px hourglass. Set by BuildBand.
        /// </summary>
        private float BandCalRowH = 28f;

        /// <summary>The calendar block: the row + the gap + the second row.</summary>
        private float BandCalH => BandCalRowH + BandCalRow2Gap + BandCalRow2H;

        /// <summary>The body grid: rows 30 / 26, row-gap 6; column 2 starts at 186 + 16.</summary>
        private const float BandBodyH = 30f + 6f + 26f, BandBodyCol2 = 186f + 16f, BandBodyRow2 = 30f + 6f;

        // ------------------------------------------------------------------ the band's elements (ExploreScene.js:286-323)

        private TMP_Text LayerTitle;
        private string ShownLayerTitle;
        private RollLabel DaysRoll, HpRoll, StRoll, MaxRoll, MarksRoll;

        /// <summary>.ocbg-explore-marks: 訓練の刻み (MarksLabel), enabled while a mark can be spent now.</summary>
        private ExPanelButton MarksBtn;

        /// <summary>手記（J） / 所持カード（C） / 設定（Esc）.</summary>
        private ExPanelButton JournalBtn, CardsBtn, SettingsBtn;

        private TMP_Text BandDaysText, BandHpText, BandStText, BandMaxText, BandMarksText;
        private RectTransform BandDayPips, BandCalNote;
        private string BandPipsKey, BandNoteKey, BandMarksShown;

        /// <summary>The label's own colour, and whether it is dimmed now (the calendar stands still).</summary>
        private Rgba BandDaysInk;
        private bool BandDaysDimmed;

        /// <summary>
        /// Builds the band into Band (ExploreScene.js:285-323): the layer, the calendar with its pips and its note, the
        /// body block with the marks button, the three buttons; adds the five RollLabels to Rollers and wires the buttons
        /// (ExploreScene.js:2270-2278: OpenOverlay, the marks' Proceed). Called once from Mount.
        /// </summary>
        // js: scenes/explore/ExploreScene.js:285
        private void BuildBand()
        {
            UiNode.Of(Band).Place(0f, 0f, ExploreStyle.StageW, ExploreStyle.BandH);
            BoxGraphic bandBox = BoxGraphic.On(Band, BandCss);
            bandBox.raycastTarget = true; // a div takes the pointer: what lies under the band is not reached through it

            // ---- the layer: stairs 28 + the layer title (redrawn only when it changes) ----
            // Every label of the band sits on its line box's Blink baseline (PanelBaseShift).
            RectTransform layer = Ui.Div(Band, "ocbg-explore-band-layer", BandLayerX, (BandInnerH - 36f) / 2f, BandLayerW, 36f);
            PanelIcon(layer, "stairs", 28f, ExploreStyle.Ink, 0f, 4f);
            // the title shrinks to what is left of the 360 px (overflow: hidden; text-overflow: ellipsis); its digits stay
            // in the display face (numsInFace), so its line is its line-height
            TextStyle titleTs = PanelText(LayerTitleCss, true);
            LayerTitle = Label.Make(layer, "", titleTs, 28f + 12f, PanelBaseShift(titleTs, PanelLineOf(titleTs, false)), BandLayerW - 28f - 12f, 36f, "ocbg-explore-layer-title");

            // ---- the calendar: hourglass 22 + 「残り N 日」, then the pips or the note ----
            // The row is as tall as the label's line box: the label always carries its number, whose mono face makes a
            // 22px/28px line 30 tall.
            TextStyle labelTs = PanelText(BandLabelCss, true);
            PanelLine labelLine = PanelLineOf(labelTs, true);
            float labelH = labelLine.H, labelShift = PanelBaseShift(labelTs, labelLine);
            BandCalRowH = Math.Max(22f, labelH);
            RectTransform cal = Ui.Div(Band, "ocbg-explore-band-calendar", BandCalX, (BandInnerH - BandCalH) / 2f, BandCalW, BandCalH);
            PanelIcon(cal, "hourglass", 22f, ExploreStyle.Ink, 0f, (BandCalRowH - 22f) / 2f);
            BandDaysText = Label.Make(cal, "", labelTs, 22f + 8f, (BandCalRowH - labelH) / 2f + labelShift, null, labelH, "ocbg-explore-band-label");
            BandDaysInk = Label.StyleOf(BandDaysText)?.Color ?? ExploreStyle.Ink;
            float row2Y = BandCalRowH + BandCalRow2Gap;
            BandDayPips = Ui.Div(cal, "ocbg-explore-pips", 0f, row2Y + (BandCalRow2H - CalPipSize) / 2f, BandCalW, CalPipSize);
            BandCalNote = Ui.Div(cal, "ocbg-explore-calendar-note", 0f, row2Y, BandCalW, BandCalRow2H);

            // ---- the body: HP, stamina / 訓練の刻み, max stamina (grid 186px 1fr, rows 30 / 26) ----
            // each row (flex, align-items: center) is the label's line box (30), centred in the 30 px grid row
            RectTransform body = Ui.Div(Band, "ocbg-explore-band-body", BandBodyX, (BandInnerH - BandBodyH) / 2f, BandBodyW, BandBodyH);
            float hpRowH = Math.Max(22f, labelH), hpRowY = (30f - hpRowH) / 2f;
            PanelIcon(body, "heart", 22f, ExploreStyle.Crimson, 0f, hpRowY + (hpRowH - 22f) / 2f);
            BandHpText = Label.Make(body, "", labelTs, 22f + 8f, hpRowY + (hpRowH - labelH) / 2f + labelShift, null, labelH, "ocbg-explore-band-label");
            PanelIcon(body, "pip", 20f, ExploreStyle.Amber, BandBodyCol2, hpRowY + (hpRowH - 20f) / 2f);
            BandStText = Label.Make(body, "", labelTs, BandBodyCol2 + 20f + 8f, hpRowY + (hpRowH - labelH) / 2f + labelShift, null, labelH, "ocbg-explore-band-label");

            // js: scenes/explore/ExploreScene.js:304 (justify-self: start; margin-left: −10px; its line box + the 1 px border,
            // centred in its 26 px row: 「訓練の刻み 0」 carries a number, so the 16px/22px line is 23 and the button 25)
            TextStyle marksTs = PanelText(MarksCss, true);
            PanelLine marksLine = PanelLineOf(marksTs, true);
            float marksLineH = marksLine.H;
            MarksBtn = ExPanelButton.Make(body, "ocbg-explore-marks", MarksCss, -10f, BandBodyRow2 + (26f - (marksLineH + 2f)) / 2f, 22f, marksLineH + 2f);
            MarksBtn.States.State(":not([disabled])", MarksEnabledCss).State(":focus-visible", PanelFocusCss).State(":not([disabled]):hover", MarksHoverCss);
            MarksBtn.Restyle = b =>
            {
                b.States.SetState(":not([disabled])", !b.Disabled, false);
                b.States.SetState(":focus-visible", b.FocusRing, false);
                b.States.SetState(":not([disabled]):hover", !b.Disabled && b.Hovered, false);
            };
            BandMarksText = Label.Make(MarksBtn.Root, "", marksTs, 1f + 10f, 1f + PanelBaseShift(marksTs, marksLine), null, marksLineH, "label");
            MarksBtn.Disabled = true;
            MarksBtn.Restate(instant: true);
            // 最大 12 = 10 + 一時 2: its line box (23 with its numbers) centred in the 26 px row
            TextStyle maxTs = PanelText(BandSmallCss, true);
            PanelLine maxLine = PanelLineOf(maxTs, true);
            BandMaxText = Label.Make(body, "", maxTs, BandBodyCol2, BandBodyRow2 + (26f - maxLine.H) / 2f + PanelBaseShift(maxTs, maxLine), null, maxLine.H, "ocbg-explore-small");

            // ---- the buttons (gap 8): right-aligned by margin-left: auto while there is room; the blocks before them are
            // flex: none and the buttons do not wrap, so when they do not fit they start right after the body block and
            // run past the band's padding (the browser's band: 1530…1907) ----
            JournalBtn = BandButton("journal", ExplorationText.JournalButton);
            CardsBtn = BandButton("cards", ExplorationText.CardsButton);
            SettingsBtn = BandButton("gear", ExplorationText.SettingsButton);
            float total = JournalBtn.Node.W + 8f + CardsBtn.Node.W + 8f + SettingsBtn.Node.W;
            float bx = Math.Max(BandBodyX + BandBodyW + BandGap, ExploreStyle.StageW - BandPadX - total);
            foreach (ExPanelButton b in new[] { JournalBtn, CardsBtn, SettingsBtn })
            {
                b.Node.X = bx;
                bx += b.Node.W + 8f;
            }

            // js: scenes/explore/ExploreScene.js:317
            DaysRoll = new RollLabel(BandDaysText);
            HpRoll = new RollLabel(BandHpText);
            StRoll = new RollLabel(BandStText);
            MaxRoll = new RollLabel(BandMaxText);
            MarksRoll = new RollLabel(BandMarksText);
            Rollers.AddRange(new[] { DaysRoll, HpRoll, StRoll, MaxRoll, MarksRoll });

            // ---- the buttons' listeners (ExploreScene.js:2270-2278) and the Tab order (DOM order) ----
            JournalBtn.Click += () => OpenOverlay(ExploreOverlayKind.Journal).Forget();
            CardsBtn.Click += () => OpenOverlay(ExploreOverlayKind.Cards).Forget();
            SettingsBtn.Click += () => OpenOverlay(ExploreOverlayKind.Settings).Forget();
            MarksBtn.Click += MarksClick;
            AddFocus(MarksBtn.Root, MarksBtn.Press, MarksBtn.SetFocusRing, () => !MarksBtn.Disabled, MarksFocusLabel);
            foreach (ExPanelButton b in new[] { JournalBtn, CardsBtn, SettingsBtn })
            {
                ExPanelButton btn = b;
                string text = btn.Text;
                AddFocus(btn.Root, btn.Press, btn.SetFocusRing, () => !btn.Disabled, () => text);
            }
        }

        /// <summary>
        /// One .ocbg-explore-bandbtn (ExploreScene.js:305-307): icon 28 over its label (400 16/20), 72 tall, padding
        /// 6 12 4, its width the label's (or the icon's) + padding + border; placed at the band's height (x set by the caller).
        /// </summary>
        private ExPanelButton BandButton(string icon, string text)
        {
            TextStyle ts = PanelText(BandBtnCss);
            float lw = TextMeasure.Width(text, ts);
            float cw = Math.Max(28f, lw);
            float w = cw + 12f + 12f + 2f, h = 72f;
            ExPanelButton b = ExPanelButton.Make(Band, "ocbg-explore-bandbtn", BandBtnCss, 0f, (BandInnerH - h) / 2f, w, h);
            b.Text = text;
            b.States.State("[disabled]", BandBtnDisabledCss).State(":focus-visible", PanelFocusCss).State(":hover", BandBtnHoverCss);
            b.Restyle = x =>
            {
                x.States.SetState("[disabled]", x.Disabled, false);
                x.States.SetState(":focus-visible", x.FocusRing, false);
                x.States.SetState(":hover", x.Hovered && !x.Disabled, false);
            };
            // column, centred: the content box is 60 tall (72 − 6 − 4 − 2), the content 28 + 4 + 20 = 52
            float top = 1f + 6f + (h - 2f - 6f - 4f - 52f) / 2f;
            PanelIcon(b.Root, icon, 28f, ExploreStyle.Ink, 1f + 12f + (cw - 28f) / 2f, top);
            Label.Make(b.Root, text, ts, 1f + 12f + (cw - lw) / 2f, top + 28f + 4f, lw, 20f, "label");
            b.Restate(instant: true);
            return b;
        }

        // js: scenes/explore/ExploreScene.js:2273
        /// <summary>The 訓練の刻み button: trial keeps the card open in the session (OpenTraining), so a reload brings it back.</summary>
        private void MarksClick()
        {
            if (!CanTrainNow()) return;
            if (Session.Trial)
            {
                if (!Session.OpenTraining()) return;
                Persist();
            }
            else PendingTraining = true;
            Proceed().Forget();
        }

        /// <summary>
        /// buttonLabel(marksBtn) (ExploreScene.js:211): the button's first span child is RollLabel's first number span, so
        /// a Tab-focused 訓練の刻み names its digits (「Enter 1」), as the JS legend does; the whole text when it has none.
        /// </summary>
        private string MarksFocusLabel()
        {
            string t = MarksRoll != null ? MarksRoll.Text ?? "" : "";
            int i = 0;
            while (i < t.Length && !IsDigit(t[i])) i++;
            if (i >= t.Length) return t;
            int j = i;
            while (j < t.Length && IsDigit(t[j])) j++;
            return t.Substring(i, j - i);
        }

        // js: scenes/explore/ExploreScene.js:752
        /// <summary>drawBand(now, animate): the band from Screen (numbers roll when animate).</summary>
        private void DrawBand(float now, bool animate)
        {
            if (Screen == null || !Band) return;
            if (Screen.LayerTitle != ShownLayerTitle)
            {
                ShownLayerTitle = Screen.LayerTitle;
                Label.SetText(LayerTitle, Rich(Screen.LayerTitle, Label.StyleOf(LayerTitle), numsInFace: true));
            }
            DrawCalendar(now, animate);

            HpRoll.Set(Screen.HpLabel, now, animate);
            StRoll.Set(Screen.StaminaLabel, now, animate);
            MaxRoll.Set(Screen.MaxStaminaLabel, now, animate);
            MarksRoll.Set(Screen.MarksLabel, now, animate);
            LayOutMarks();
            SyncMarks();
            CardsBtn.Disabled = !Screen.CanOpenCards;
            SettingsBtn.Disabled = !Screen.CanOpenSettings;
        }

        /// <summary>
        /// The calendar (§6.2): 「残り N 日」 rolls like the other numbers; a calendar that stands still dims its label and
        /// shows the bridge's note in place of the pips.
        /// </summary>
        private void DrawCalendar(float now, bool animate)
        {
            DaysRoll.Set(Screen.DaysLabel ?? "", now, animate);
            bool frozen = Screen.CalendarFrozen;
            if (frozen != BandDaysDimmed)
            {
                BandDaysDimmed = frozen;
                Label.SetColor(BandDaysText, frozen ? ExploreStyle.Steel : BandDaysInk);
            }
            string note = frozen ? Screen.CalendarNote ?? "" : "";
            DrawCalendarNote(note);
            DrawDayPips(note.Length > 0 ? 0 : Screen.DaysLeft, frozen, note.Length > 0);
        }

        /// <summary>
        /// The coming month as pips (§6.4): one lit pip per day left, up to a month; the last lit one, the day the next new
        /// node takes, faint; all steel while the calendar stands still. Rebuilt only when what they show changed (new
        /// pips never transition). Hidden while the note takes their row.
        /// </summary>
        private void DrawDayPips(int daysLeft, bool frozen, bool hidden)
        {
            int lit = Math.Max(0, Math.Min(CalendarPips, daysLeft));
            string key = hidden ? "-" : lit.ToString(CultureInfo.InvariantCulture) + (frozen ? "/f" : "/r");
            if (key == BandPipsKey) return;
            BandPipsKey = key;
            Ui.Clear(BandDayPips);
            if (hidden) return;
            for (int i = 0; i < CalendarPips; i++)
            {
                bool on = i < lit;
                bool next = on && !frozen && i == lit - 1;
                string css = PipCss + PipLongCss + (!on ? "" : frozen ? PipFrozenCss : next ? PipNextCss : PipLeftCss);
                BoxGraphic.Make(BandDayPips, css, i * (CalPipSize + CalPipGap), 0f, CalPipSize, CalPipSize, next ? "is-next" : on ? "is-left" : "i");
            }
        }

        /// <summary>The note of a calendar that stands still, on the pips' row; "" clears it.</summary>
        private void DrawCalendarNote(string note)
        {
            if (note == BandNoteKey) return;
            BandNoteKey = note;
            Ui.Clear(BandCalNote);
            if (note.Length == 0) return;
            TextStyle ts = PanelText(CalendarNoteCss, true);
            PanelLine line = PanelLineOf(ts, HasDigits(note));
            string text = Rich(note, ts);
            float w = Math.Min(BandCalW, TextMeasure.Width(text, ts));
            Label.Make(BandCalNote, text, ts, 0f, (BandCalRow2H - line.H) / 2f + PanelBaseShift(ts, line), w, line.H, "label");
        }

        /// <summary>The marks button sized to its label (max-content + padding 0 10 + border 1).</summary>
        private void LayOutMarks()
        {
            if (MarksBtn == null || !BandMarksText) return;
            LabelState st = Label.StateOf(BandMarksText);
            BandMarksShown = st.Text;
            float lw = TextMeasure.Width(st.Text ?? "", st.Style ?? PanelText(MarksCss, true));
            Ui.Size(BandMarksText.rectTransform, lw, BandMarksText.rectTransform.sizeDelta.y);
            MarksBtn.Node.W = lw + 10f + 10f + 2f;
        }

        /// <summary>
        /// The clock ticks the rollers, then this: the box around a rolled label (the marks button) follows its width as
        /// the DOM reflows around RollLabel's spans.
        /// </summary>
        private void FollowRolledBoxes()
        {
            if (!BandMarksText) return;
            if (Label.StateOf(BandMarksText).Text != BandMarksShown) LayOutMarks();
        }

        // js: scenes/explore/ExploreScene.js:893
        /// <summary>
        /// The training mark can be spent now: a mark is left and nothing waits, on the map, under the layer-break card or
        /// at the interlude (the slots' rule, W-8).
        /// </summary>
        private bool CanTrainNow() =>
            Session.State.TrainingMarks > 0 && Session.IsIdle && !Session.State.IsOver && !Busy && !ConfirmOpen &&
            !FlowRunning && (Plate == null || Plate.Kind == ExploreStyle.PlateScreen);

        // js: scenes/explore/ExploreScene.js:897
        /// <summary>
        /// marksBtn.disabled = !canTrainNow(). The carried panel's is-held follows the plate at the same moments (openPlate
        /// and closePlate toggle it right before they call syncMarks; render keeps it), so it is synced here too.
        /// </summary>
        private void SyncMarks()
        {
            if (MarksBtn != null) MarksBtn.Disabled = !CanTrainNow();
            SyncHeld();
        }

        // js: scenes/explore/ExploreScene.js:903
        /// <summary>
        /// 「この生を終える」 can be pressed at any time while exploring — except under a card that waits on the player or a
        /// confirmation: there it is held, and looks held (disabled).
        /// </summary>
        private void SyncEndLife()
        {
            if (Screen == null || EndLifeBtn == null) return;
            SetDisabled(EndLifeBtn, !Screen.CanEndLife || Busy || ConfirmOpen || (Plate != null && Plate.Kind != ExploreStyle.PlateScreen));
        }

        // ------------------------------------------------------------------ helpers shared by the band, the detail and the carried items

        /// <summary>The scene's text styles by their CSS (parsed once per scene); rich = TMP rich text on (Rich / RollLabel markup).</summary>
        private readonly Dictionary<string, TextStyle> PanelStyleCache = new Dictionary<string, TextStyle>(StringComparer.Ordinal);

        /// <summary>TextStyle.FromCss(css), cached; rich turns TMP rich text on (Rich, RollLabel); align overrides text-align.</summary>
        private TextStyle PanelText(string css, bool rich = false, TextAlign? align = null)
        {
            string key = (rich ? "R" : "P") + (align.HasValue ? ((int)align.Value).ToString(CultureInfo.InvariantCulture) : "-") + "|" + css;
            if (PanelStyleCache.TryGetValue(key, out TextStyle s)) return s;
            s = TextStyle.FromCss(css);
            if (rich) s.RichText = true;
            if (align.HasValue) s.Align = align.Value;
            PanelStyleCache[key] = s;
            return s;
        }

        /// <summary>One CSS line box: its height and its baseline below its top.</summary>
        private readonly struct PanelLine
        {
            public readonly float H, Base;

            public PanelLine(float h, float baseline)
            {
                H = h;
                Base = baseline;
            }
        }

        /// <summary>
        /// One line of `s` as Blink lays it out: each face's ascent and descent rounded to whole px (SkScalarRound), the
        /// half-leading floored onto the ascent (descent = line-height − ascent), the line box from the highest ascent to
        /// the lowest descent of the faces on it. With `digits` the line also holds rich()'s / RollLabel's digit runs
        /// (.ocbg-explore-num: IBM Plex Mono 600 at the same size and line-height), whose face reaches deeper below the
        /// baseline: such a line is taller than its line-height — 22px/28px 30 (the band's 刻限 7 / 10), 20px/28px 29 (a
        /// detail value), 18px/26px 28 (a forecast line), 17px/22px and 16px/22px 23 (a tool chip's effect, a slot's
        /// forecast). Measured on the reference shots: the band's pips (top 62) and gauge (59), the option boxes (256 tall
        /// at a rest node), the tool chips (29). The faces' table metrics (TextMath.Metrics) are what Blink reads.
        /// </summary>
        private static PanelLine PanelLineOf(TextStyle s, bool digits)
        {
            float lh = s.LineHeightPx;
            BlinkFace(s.Family, s.Weight, s.Size, lh, out float a, out float d);
            if (digits)
            {
                BlinkFace(Family.Mono, 600, s.Size, lh, out float ma, out float md);
                a = Math.Max(a, ma);
                d = Math.Max(d, md);
            }
            return new PanelLine(a + d, a);
        }

        private static void BlinkFace(Family family, int weight, float size, float lineHeight, out float ascent, out float descent)
        {
            FontMetrics m = TextMath.Metrics(family, weight);
            float a = (float)Math.Floor(m.AscentEm * size + 0.5), d = (float)Math.Floor(Math.Abs(m.DescentEm) * size + 0.5);
            ascent = a + (float)Math.Floor((lineHeight - (a + d)) / 2f);
            descent = lineHeight - ascent;
        }

        /// <summary>
        /// How far below a line box's top a label of `s` is placed so that its baseline (Label: the CSS baseline with the
        /// faces' exact metrics) lands on the line's Blink baseline (whole-px metrics): 22px/28px +0.41.
        /// </summary>
        private static float PanelBaseShift(TextStyle s, PanelLine line) => line.Base - TextMath.CssBaselineFromTop(s.Spec);

        /// <summary>Whether a line holds a digit run (rich() sets it in the mono face).</summary>
        private static bool HasDigits(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
                if (IsDigit(c)) return true;
            return false;
        }

        /// <summary>
        /// A rich() text that may wrap, at a width: its height (lines × its line box: a text with numbers is taken to carry
        /// them on each line it wraps to, as the bridge's wrapped lines do) and the style to draw it with — the line box as
        /// the line pitch when it differs from the line-height — with the y shift that keeps the first baseline where `s`
        /// puts it (the extra height of a line with numbers lies under its baseline).
        /// </summary>
        private static float PanelRichBlock(string markup, TextStyle s, float width, bool digits, out TextStyle draw, out float yShift)
        {
            int lines = TextMeasure.Lines(markup, s, width);
            float lineH = PanelLineOf(s, digits).H;
            draw = s;
            yShift = 0f;
            if (lines > 1 && Math.Abs(lineH - s.LineHeightPx) > 0.001f)
            {
                draw = s.Clone();
                draw.LineHeight = lineH;
                yShift = -(lineH - s.LineHeightPx) / 2f;
            }
            return lines * lineH;
        }

        /// <summary>ico(name, size, colour, opts) placed at (x, y) of parent.</summary>
        private static PathGraphic PanelIcon(RectTransform parent, string name, float size, Rgba color, float x, float y, IcoOpts opts = null)
        {
            PathGraphic g = ExploreIcons.Ico(parent, name, size, color, opts);
            Ui.Place(g.rectTransform, x, y);
            return g;
        }

        /// <summary>One flex item of a wrapping row: its element (placed at (0, 0) of the row) and its border-box size.</summary>
        private struct PanelItem
        {
            public RectTransform El;
            public float W, H;

            public PanelItem(RectTransform el, float w, float h)
            {
                El = el;
                W = w;
                H = h;
            }
        }

        /// <summary>
        /// display: flex; flex-wrap: wrap; align-items: center — the items of a row placed left to right, a line broken
        /// before an item that would pass `width`, each item centred in its line. Returns the rows' height.
        /// </summary>
        private static float PanelWrap(List<PanelItem> items, float width, float columnGap, float rowGap)
        {
            float y = 0f;
            int start = 0;
            while (start < items.Count)
            {
                float x = 0f, lineH = 0f;
                int end = start;
                while (end < items.Count)
                {
                    PanelItem it = items[end];
                    if (end > start && x + it.W > width + 0.01f) break;
                    x += it.W + columnGap;
                    lineH = Math.Max(lineH, it.H);
                    end++;
                }
                x = 0f;
                for (int i = start; i < end; i++)
                {
                    PanelItem it = items[i];
                    if (it.El) Ui.Place(it.El, x, y + (lineH - it.H) / 2f);
                    x += it.W + columnGap;
                }
                y += lineH;
                start = end;
                if (start < items.Count) y += rowGap;
            }
            return y;
        }

        /// <summary>
        /// A declaration block without its width / height: the panels lay their boxes out here (the flex and grid boxes),
        /// and CssStates would otherwise write the rule's size into the element (`height: auto` resolves to 0 there).
        /// </summary>
        private static string PanelBoxCss(string css)
        {
            var sb = new StringBuilder(css.Length);
            int depth = 0, start = 0;
            for (int i = 0; i <= css.Length; i++)
            {
                char c = i < css.Length ? css[i] : ';';
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ';' && depth <= 0)
                {
                    string d = css.Substring(start, i - start).Trim();
                    int colon = d.IndexOf(':');
                    string prop = colon > 0 ? d.Substring(0, colon).Trim().ToLowerInvariant() : "";
                    if (d.Length > 0 && prop != "width" && prop != "height") sb.Append(d).Append("; ");
                    start = i + 1;
                }
            }
            return sb.ToString();
        }

        /// <summary>Removes the panel's focusables from the scene's Tab scope (their elements are about to be rebuilt).</summary>
        private void DropPanelFocus(List<Focusable> list)
        {
            foreach (Focusable f in list.ToArray())
            {
                Scope?.Remove(f);
                FocusLabels.Remove(f);
            }
            list.Clear();
        }

        /// <summary>
        /// A scene button that is not an .ocbg-btn (the band's buttons, 訓練の刻み, the consumable slots): its box (the
        /// element's rules through CssStates, with their transitions, on the document clock), pointer listeners, and the
        /// flags the stylesheet reads (:hover from the pointer, [disabled], :focus-visible from Tab). Restyle turns the
        /// element's states on or off from the flags (SetState(…, apply: false)); Restate applies them. Click is onclick
        /// (nothing while disabled); Press() is b.click().
        /// </summary>
        private sealed class ExPanelButton
        {
            private bool _disabled, _hover, _focus;

            public RectTransform Root { get; private set; }
            public UiNode Node { get; private set; }
            public BoxGraphic Box { get; private set; }
            public ClickArea Area { get; private set; }
            public CssStates States { get; private set; }

            /// <summary>The button's own words (what Enter presses).</summary>
            public string Text { get; set; } = "";

            /// <summary>onclick.</summary>
            public event Action Click;

            /// <summary>Sets the states that follow the flags (and any class of the owner) without applying them.</summary>
            public Action<ExPanelButton> Restyle;

            public static ExPanelButton Make(RectTransform parent, string name, string baseCss, float x, float y, float w, float h)
            {
                var b = new ExPanelButton();
                b.Root = Ui.Div(parent, name, x, y, w, h);
                b.Node = UiNode.Of(b.Root);
                b.Box = BoxGraphic.On(b.Root);
                b.Area = ClickArea.On(b.Box);
                b.Area.Click += _ => b.Press();
                b.Area.HoverChanged += on =>
                {
                    b._hover = on;
                    b.Restate();
                };
                b.States = CssStates.Of(b.Root);
                b.States.Base(baseCss);
                return b;
            }

            /// <summary>[disabled]: no clicks, Tab skips it.</summary>
            public bool Disabled
            {
                get => _disabled;
                set
                {
                    if (_disabled == value) return;
                    _disabled = value;
                    Restate();
                }
            }

            /// <summary>The pointer is over it (CSS :hover).</summary>
            public bool Hovered => _hover;

            /// <summary>Tab moved the focus to it (CSS :focus-visible).</summary>
            public bool FocusRing => _focus;

            public void SetFocusRing(bool on)
            {
                _focus = on;
                Restate();
            }

            /// <summary>The states again from the flags; instant = no transitions.</summary>
            public void Restate(bool instant = false)
            {
                if (!Root) return;
                Restyle?.Invoke(this);
                States.Apply(instant);
            }

            /// <summary>b.click(): onclick runs unless the button is disabled; the microtasks run after it.</summary>
            public void Press()
            {
                if (_disabled || !Root) return;
                try { Click?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                Microtasks.Drain();
            }
        }
    }
}
