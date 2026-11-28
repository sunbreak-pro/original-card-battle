// The shared header band (#1074): one 96 px band at the top of the base, 「支度の卓」, the exploration and the battle,
// drawn after the design canvas's 「共通ヘッダー」. The band computes nothing: what it shows is Trial.Bridge's
// HeaderContent, where it stands is HeaderBand, and the texts' widths it measures are all it adds. What a button opens
// is the screen's, handed in as HeaderActions.
//
// The left side (the place, the rule, the calendar, HP, stamina) is laid out again after every change, because a
// number that gains a digit moves everything after it. The buttons keep their places unless a word changes.
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Header
{
    /// <summary>What the band's buttons open. A null action leaves its button standing but deaf.</summary>
    public sealed class HeaderActions
    {
        public Action OnSpeed, OnOwnedCards, OnJournal, OnSettings;

        /// <summary>The table's chip back to the base.</summary>
        public Action OnBack;

        internal Action Of(HeaderButtonKind kind)
        {
            switch (kind)
            {
                case HeaderButtonKind.Speed: return OnSpeed;
                case HeaderButtonKind.OwnedCards: return OnOwnedCards;
                case HeaderButtonKind.Journal: return OnJournal;
                case HeaderButtonKind.Settings: return OnSettings;
                default: return null;
            }
        }
    }

    /// <summary>One Tab stop of the band and what it is.</summary>
    public readonly struct HeaderTabStop
    {
        public HeaderTabStop(HeaderStop kind, Focusable focus)
        {
            Kind = kind;
            Focus = focus;
        }

        public HeaderStop Kind { get; }
        public Focusable Focus { get; }
    }

    public sealed class HeaderBar
    {
        private static readonly HeaderButtonKind[] Kinds =
        {
            HeaderButtonKind.Speed, HeaderButtonKind.OwnedCards, HeaderButtonKind.Journal, HeaderButtonKind.Settings,
        };

        /// <summary>The keyboard focus ring (the exploration band's :focus-visible).</summary>
        private const string FocusCss = "outline: 2px solid var(--wick); outline-offset: 2px;";

        private readonly HeaderActions _actions;
        private readonly RectTransform _tipLayer;
        private readonly RectTransform _root;
        private HeaderContent _content;

        private readonly TextStyle _nameS, _tagS, _wordsS, _daysS, _noteS, _numberS, _buttonS, _badgeS, _tipS;

        // the left side
        private readonly RectTransform _place, _tags, _calendar, _hpGroup, _staminaGroup;
        private readonly PathGraphic _placeIcon, _calendarIcon, _heart;
        private readonly TextMeshProUGUI _name, _daysLead, _days, _daysTail, _note, _hp, _hpTail, _stamina, _staminaTail;
        private readonly BoxGraphic _divider, _pip;
        private readonly Chip _back;
        private readonly List<(RectTransform Box, TextMeshProUGUI Text)> _tagParts = new List<(RectTransform, TextMeshProUGUI)>();
        private HeaderPlaceArt? _shownArt;
        private bool? _shownFrozen;

        // the right side
        private readonly Dictionary<HeaderButtonKind, Button> _buttons = new Dictionary<HeaderButtonKind, Button>();
        private readonly BoxGraphic _separator;
        private readonly RectTransform _badge;
        private readonly TextMeshProUGUI _badgeText;
        private RectTransform _tip;
        private object _tipOwner;

        private HeaderBar(RectTransform parent, HeaderContent content, HeaderActions actions, RectTransform tipLayer, bool blockPointer)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _actions = actions ?? new HeaderActions();
            _tipLayer = tipLayer;

            _nameS = Ocb.TextOf(HeaderBand.Css.PlaceName);
            _tagS = Ocb.TextOf(HeaderBand.Css.TagText);
            _wordsS = Ocb.TextOf(HeaderBand.Css.Words);
            _daysS = Ocb.TextOf(HeaderBand.Css.Days);
            _noteS = Ocb.TextOf(HeaderBand.Css.FrozenNote);
            _numberS = Ocb.TextOf(HeaderBand.Css.Number);
            _buttonS = Ocb.TextOf(HeaderBand.Css.ButtonLabel);
            _badgeS = Ocb.TextOf(HeaderBand.Css.BadgeText);
            _tipS = Ocb.TextOf(HeaderBand.Css.TipText);

            TableBox band = HeaderBand.Band;
            _root = Ui.Div(parent, "ocbg-header", band.X, band.Y, band.W, band.H);
            BoxGraphic bandBox = BoxGraphic.On(_root, HeaderBand.Css.Band);
            // a div takes the pointer: what lies under the band is not reached through it
            bandBox.raycastTarget = blockPointer;

            // ---- the place ----
            _place = Ui.Div(_root, "place", 0f, 0f, 0f, HeaderBand.InnerH);
            _placeIcon = PathGraphic.Make(_place, 0f, (HeaderBand.InnerH - HeaderBand.PlaceIcon) / 2f, HeaderBand.PlaceIcon, HeaderBand.PlaceIcon, "icon");
            _name = Label.Make(_place, "", _nameS, 0f, 0f, null, null, "name");
            _tags = Ui.Div(_place, "tags", 0f, 0f, 0f, HeaderBand.InnerH);
            _back = new Chip(this, _place);
            _divider = BoxGraphic.Make(_root, HeaderBand.Css.Divider, 0f, 0f, HeaderBand.DividerW, HeaderBand.DividerH, "divider");

            // ---- the calendar ----
            _calendar = Ui.Div(_root, "calendar", 0f, 0f, 0f, HeaderBand.InnerH);
            _calendarIcon = Icon(_calendar, "hourglass", HeaderBand.Icons.Hourglass, false, HeaderBand.CalendarIcon, DsTokens.Ink);
            _daysLead = Label.Make(_calendar, HeaderBand.DaysLead, _wordsS, 0f, 0f, null, null, "lead");
            _days = Label.Make(_calendar, "", _daysS, 0f, 0f, null, null, "days");
            _daysTail = Label.Make(_calendar, HeaderBand.DaysTail, _wordsS, 0f, 0f, null, null, "tail");
            _note = Label.Make(_calendar, "", _noteS, 0f, 0f, null, null, "note");

            // ---- HP and stamina ----
            _hpGroup = Ui.Div(_root, "hp", 0f, 0f, 0f, HeaderBand.InnerH);
            _heart = Icon(_hpGroup, "heart", HeaderBand.Icons.Heart, true, HeaderBand.HeartIcon, DsTokens.Crimson);
            _hp = Label.Make(_hpGroup, "", _numberS, 0f, 0f, null, null, "number");
            _hpTail = Label.Make(_hpGroup, "", _wordsS, 0f, 0f, null, null, "tail");
            _staminaGroup = Ui.Div(_root, "stamina", 0f, 0f, 0f, HeaderBand.InnerH);
            _pip = BoxGraphic.Make(_staminaGroup, HeaderBand.Css.StaminaPip, 0f, (HeaderBand.InnerH - HeaderBand.StaminaPip) / 2f,
                HeaderBand.StaminaPip, HeaderBand.StaminaPip, "pip");
            _stamina = Label.Make(_staminaGroup, "", _numberS, 0f, 0f, null, null, "number");
            _staminaTail = Label.Make(_staminaGroup, "", _wordsS, 0f, 0f, null, null, "tail");

            // ---- the buttons, the separator, the deck's count (over the owned cards' corner, so it is not dimmed with them) ----
            foreach (HeaderButtonKind kind in Kinds) _buttons[kind] = new Button(this, kind);
            _separator = BoxGraphic.Make(_root, HeaderBand.Css.Separator, 0f, 0f, HeaderBand.SeparatorW, HeaderBand.SeparatorH, "separator");
            _badge = Ui.Div(_root, "count", 0f, 0f, HeaderBand.BadgeMinW, HeaderBand.BadgeH);
            BoxGraphic.On(_badge, HeaderBand.Css.Badge);
            _badgeText = Label.Make(_badge, "", _badgeS, 0f, 0f, null, null, "text");

            Show(_content);
        }

        /// <summary>
        /// The band at the top of <paramref name="parent"/> (a 1920 × 1080 stage layer), showing <paramref name="content"/>.
        /// A button's bubble opens in <paramref name="tipLayer"/> when given (a layer over the screen's own, on the same
        /// stage origin), else in the band. <paramref name="blockPointer"/> false lets the pointer through the band's
        /// empty parts (only the buttons and the chip take it).
        /// </summary>
        public static HeaderBar Create(RectTransform parent, HeaderContent content, HeaderActions actions = null,
            RectTransform tipLayer = null, bool blockPointer = true) => new HeaderBar(parent, content, actions, tipLayer, blockPointer);

        /// <summary>The band (its node is the screen's to raise or hide).</summary>
        public RectTransform Root => _root ? _root : null;

        /// <summary>What the band shows now. Change it through the setters, which redraw.</summary>
        public HeaderContent Content => _content;

        /// <summary>The band's Tab stops in order (the chip back, the speed, the owned cards, the journal, the settings), each with its kind.</summary>
        public IReadOnlyList<HeaderTabStop> Stops
        {
            get
            {
                var list = new List<HeaderTabStop> { new HeaderTabStop(HeaderStop.Back, _back.Focus) };
                foreach (HeaderButtonKind kind in Kinds) list.Add(new HeaderTabStop(HeaderBand.StopOf(kind), _buttons[kind].Focus));
                return list;
            }
        }

        /// <summary>The band's Tab stops in <see cref="Stops"/>' order, for a screen that needs no kinds.</summary>
        public IReadOnlyList<Focusable> Focusables
        {
            get
            {
                var list = new List<Focusable>();
                foreach (HeaderTabStop stop in Stops) list.Add(stop.Focus);
                return list;
            }
        }

        /// <summary>The elements that take the pointer (the battle marks them as its hud's).</summary>
        public IReadOnlyList<RectTransform> PointerTargets
        {
            get
            {
                var list = new List<RectTransform> { _root, _back.Root };
                foreach (HeaderButtonKind kind in Kinds) list.Add(_buttons[kind].Root);
                return list;
            }
        }

        // ---- the setters ---------------------------------------------------------------------------------------

        /// <summary>Everything again from <paramref name="content"/>.</summary>
        public void Show(HeaderContent content)
        {
            if (!_root) return;
            _content = content ?? throw new ArgumentNullException(nameof(content));
            ShowTags(_content.Tags);
            foreach (HeaderButtonKind kind in Kinds) _buttons[kind].SetState(_content.StateOf(kind));
            _buttons[HeaderButtonKind.Speed].SetWord(HeaderBand.WordOf(HeaderButtonKind.Speed, _content.SpeedText));
            Layout();
        }

        public void SetPlace(string name, HeaderPlaceArt art)
        {
            _content.PlaceName = name ?? "";
            _content.PlaceArt = art;
            Layout();
        }

        /// <summary>The tags after the name (the battle's turn: HeaderBand.BattleTags).</summary>
        public void SetTags(IReadOnlyList<string> tags)
        {
            _content.Tags = tags ?? Array.Empty<string>();
            ShowTags(_content.Tags);
            Layout();
        }

        /// <summary>Null leaves the calendar out: HP and stamina close up against the rule.</summary>
        public void SetCalendar(HeaderCalendar calendar)
        {
            _content.Calendar = calendar;
            Layout();
        }

        public void SetHp(int hp, int maxHp)
        {
            _content.Hp = hp;
            _content.MaxHp = maxHp;
            Layout();
        }

        public void SetStamina(int stamina, int maxStamina)
        {
            _content.Stamina = stamina;
            _content.MaxStamina = maxStamina;
            Layout();
        }

        public void SetDeckCount(int count)
        {
            _content.DeckCount = count;
            Layout();
        }

        /// <summary>A button's standing, press and light (HeaderContent.OwnedCardsState for the owned cards).</summary>
        public void SetButton(HeaderButtonKind kind, HeaderButtonState state)
        {
            _content.SetState(kind, state);
            _buttons[kind].SetState(state);
            Layout();
        }

        /// <summary>The speed's word (HeaderBand.SpeedLabel).</summary>
        public void SetSpeed(string text)
        {
            _content.SpeedText = text ?? "";
            _buttons[HeaderButtonKind.Speed].SetWord(HeaderBand.WordOf(HeaderButtonKind.Speed, _content.SpeedText));
            Layout();
        }

        public void Destroy()
        {
            HideTip(null);
            if (_root) Ui.Remove(_root);
        }

        // ---- layout -------------------------------------------------------------------------------------------

        private void Layout()
        {
            if (!_root) return;
            HeaderContent c = _content;

            // the place: picture, name, tags, the chip back
            if (_shownArt != c.PlaceArt)
            {
                _shownArt = c.PlaceArt;
                _placeIcon.SetIcon(new IconDef("header-place", HeaderBand.Icons.Of(c.PlaceArt), IconDef.Box24, false), DsTokens.Ink);
            }
            Label.SetText(_name, c.PlaceName ?? "");
            float nameW = Ocb.TextWidth(c.PlaceName ?? "", _nameS);
            var tagWs = new List<float>();
            foreach ((RectTransform Box, TextMeshProUGUI Text) part in _tagParts) tagWs.Add(UiNode.Of(part.Box).W);
            float backW = c.BackChip ? _back.Width : 0f;
            _back.Visible = c.BackChip;
            float[] px = HeaderBand.PlaceXs(nameW, tagWs, backW);
            Ui.Place(_placeIcon.rectTransform, px[0], (HeaderBand.InnerH - HeaderBand.PlaceIcon) / 2f);
            PlaceRun(_name, _nameS, px[1], Baseline(_nameS));
            for (int i = 0; i < _tagParts.Count; i++) UiNode.Of(_tagParts[i].Box).Place(px[2 + i], (HeaderBand.InnerH - HeaderBand.TagH) / 2f);
            if (c.BackChip) _back.Node.Place(px[px.Length - 1], (HeaderBand.InnerH - HeaderBand.TagH) / 2f);
            float placeW = HeaderBand.PlaceW(HeaderBand.PlaceContentW(nameW, tagWs, backW), HeaderBand.PlaceWidth);

            // HP and stamina
            float hpW = Figure(_hpGroup, _heart.rectTransform, HeaderBand.HeartIcon, c.HpFigure, _hp, _hpTail);
            float stW = Figure(_staminaGroup, _pip.rectTransform, HeaderBand.StaminaPip, c.StaminaFigure, _stamina, _staminaTail);

            // the buttons' row first: the left side checks itself against its left edge
            Button speed = _buttons[HeaderButtonKind.Speed];
            HeaderButtonRow row = HeaderBand.ButtonRow(speed.State.Shown ? speed.Width : 0f, _buttons[HeaderButtonKind.OwnedCards].Width,
                _buttons[HeaderButtonKind.Journal].Width, _buttons[HeaderButtonKind.Settings].Width);

            // the calendar: steel and its note while it stands still; none at all when the screen hands none. A long note
            // that would run under the buttons gives way to the short one; c.Calendar keeps the long one, so the band
            // goes back to it once the row has room again.
            bool hasCalendar = c.HasCalendar;
            UiNode.Of(_calendar).Visible = hasCalendar;
            float calW = hasCalendar ? LayoutCalendar(c.Calendar) : 0f;
            HeaderLeftRow left = HeaderBand.LeftRow(placeW, calW, hpW, stW, hasCalendar);
            if (hasCalendar && !HeaderBand.Fits(left, row.Left))
            {
                HeaderCalendar shortened = c.Calendar.Shortened();
                if (shortened.ShownNote != c.Calendar.ShownNote)
                {
                    calW = LayoutCalendar(shortened);
                    left = HeaderBand.LeftRow(placeW, calW, hpW, stW, hasCalendar);
                }
            }
            Ui.Place(_place, left.PlaceX, 0f, placeW, HeaderBand.InnerH);
            Ui.Place(_divider.rectTransform, left.Divider.X, left.Divider.Y, left.Divider.W, left.Divider.H);
            if (left.HasCalendar) Ui.Place(_calendar, left.CalendarX, 0f, calW, HeaderBand.InnerH);
            Ui.Place(_hpGroup, left.HpX, 0f, hpW, HeaderBand.InnerH);
            Ui.Place(_staminaGroup, left.StaminaX, 0f, stW, HeaderBand.InnerH);

            // the buttons, right-aligned; the count on the owned cards' corner
            foreach (HeaderButtonKind kind in Kinds)
            {
                Button b = _buttons[kind];
                b.Node.Visible = b.State.Shown && row.Has(kind);
                if (row.Has(kind)) b.Place(row.Box(kind));
            }
            UiNode.Of(_separator).Visible = row.Separator.HasValue;
            if (row.Separator.HasValue)
            {
                TableBox s = row.Separator.Value;
                Ui.Place(_separator.rectTransform, s.X, s.Y, s.W, s.H);
            }
            Button owned = _buttons[HeaderButtonKind.OwnedCards];
            UiNode.Of(_badge).Visible = owned.State.Shown;
            string count = c.DeckBadge;
            Label.SetText(_badgeText, count);
            float countW = Ocb.TextWidth(count, _badgeS);
            TableBox badge = HeaderBand.BadgeBox(row.Box(HeaderButtonKind.OwnedCards), countW);
            Ui.Place(_badge, badge.X, badge.Y, badge.W, badge.H);
            Ui.Place(_badgeText.rectTransform, (badge.W - countW) / 2f, (badge.H - _badgeS.LineHeightPx) / 2f);
            _badge.SetAsLastSibling();
        }

        /// <summary>The calendar's runs laid out in its group (the days steel while frozen, the note beside them). Its width.</summary>
        private float LayoutCalendar(HeaderCalendar calendar)
        {
            HeaderFigure days = HeaderBand.DaysFigure(calendar.DaysLeft);
            string note = calendar.ShownNote;
            bool frozen = calendar.Frozen;
            if (_shownFrozen != frozen)
            {
                _shownFrozen = frozen;
                Label.SetColor(_days, frozen ? DsTokens.Steel : DsTokens.Ink);
                _calendarIcon.IconColor = frozen ? DsTokens.Steel : DsTokens.Ink;
            }
            Label.SetText(_days, days.Number);
            Label.SetText(_note, note);
            float leadW = Ocb.TextWidth(days.Lead, _wordsS), daysW = Ocb.TextWidth(days.Number, _daysS), tailW = Ocb.TextWidth(days.Tail, _wordsS);
            float noteW = note.Length > 0 ? Ocb.TextWidth(note, _noteS) : 0f;
            float[] cx = HeaderBand.FigureXs(HeaderBand.CalendarIcon, days, leadW, daysW, tailW, noteW);
            float calBase = Baseline(_wordsS, _daysS, _noteS);
            Ui.Place(_calendarIcon.rectTransform, cx[0], (HeaderBand.InnerH - HeaderBand.CalendarIcon) / 2f);
            PlaceRun(_daysLead, _wordsS, cx[1], calBase);
            PlaceRun(_days, _daysS, cx[2], calBase);
            PlaceRun(_daysTail, _wordsS, cx[3], calBase);
            PlaceRun(_note, _noteS, cx[4], calBase);
            UiNode.Of(_note).Visible = note.Length > 0;
            return HeaderBand.FigureW(HeaderBand.CalendarIcon, days, leadW, daysW, tailW, noteW);
        }

        /// <summary>A number figure (HP, stamina) laid out in its group: the picture, the number, the tail. Its width.</summary>
        private float Figure(RectTransform group, RectTransform icon, float iconW, HeaderFigure f, TextMeshProUGUI number, TextMeshProUGUI tail)
        {
            Label.SetText(number, f.Number);
            Label.SetText(tail, f.Tail);
            float nw = Ocb.TextWidth(f.Number, _numberS), tw = Ocb.TextWidth(f.Tail, _wordsS);
            float[] xs = HeaderBand.FigureXs(iconW, f, 0f, nw, tw);
            float baseline = Baseline(_numberS, _wordsS);
            Ui.Place(icon, xs[0], (HeaderBand.InnerH - iconW) / 2f);
            PlaceRun(number, _numberS, xs[2], baseline);
            PlaceRun(tail, _wordsS, xs[3], baseline);
            return HeaderBand.FigureW(iconW, f, 0f, nw, tw);
        }

        private void ShowTags(IReadOnlyList<string> tags)
        {
            if (!_tags) return;
            Ui.Clear(_tags);
            _tagParts.Clear();
            if (tags == null) return;
            foreach (string tag in tags)
            {
                float tw = Ocb.TextWidth(tag ?? "", _tagS);
                RectTransform box = Ui.Div(_tags, "tag", 0f, 0f, HeaderBand.TagW(tw), HeaderBand.TagH);
                BoxGraphic.On(box, HeaderBand.Css.Tag);
                TextMeshProUGUI text = Label.Make(box, tag ?? "", _tagS, HeaderBand.TagBorder + HeaderBand.TagPadX, HeaderBand.TagBorder, null, null, "text");
                _tagParts.Add((box, text));
            }
        }

        /// <summary>
        /// The baseline the runs of a group share (inline layout in a flex item centred in the band): the line is as
        /// tall as the deepest ascent plus the deepest descent of the faces on it, centred in the content box.
        /// </summary>
        private static float Baseline(params TextStyle[] styles)
        {
            float ascent = 0f, descent = 0f;
            foreach (TextStyle s in styles)
            {
                float a = TextMath.CssBaselineFromTop(s.Spec);
                ascent = Math.Max(ascent, a);
                descent = Math.Max(descent, s.LineHeightPx - a);
            }
            return (HeaderBand.InnerH - (ascent + descent)) / 2f + ascent;
        }

        /// <summary>A label placed so that its baseline lands on <paramref name="baseline"/>.</summary>
        private static void PlaceRun(TextMeshProUGUI label, TextStyle s, float x, float baseline) =>
            Ui.Place(label.rectTransform, x, baseline - TextMath.CssBaselineFromTop(s.Spec));

        private static PathGraphic Icon(RectTransform parent, string name, string d, bool filled, float size, Rgba color) =>
            PathGraphic.Icon(parent, new IconDef("header-" + name, d, IconDef.Box24, filled), size, color);

        // ---- the bubble ---------------------------------------------------------------------------------------

        /// <summary>The one bubble: under the band, right edge on the button's, never taking the pointer.</summary>
        private void ShowTip(object owner, TableBox button, string text)
        {
            HideTip(null);
            if (string.IsNullOrEmpty(text) || !_root) return;
            RectTransform layer = _tipLayer ? _tipLayer : _root;
            float tw = Ocb.TextWidth(text, _tipS);
            TableBox box = HeaderBand.TipBox(button, tw);
            _tip = Ui.Div(layer, "ocbg-header-tip", box.X, box.Y, box.W, box.H);
            BoxGraphic.On(_tip, HeaderBand.Css.Tip);
            Label.Make(_tip, text, _tipS, HeaderBand.TipBorder + HeaderBand.TipPadX, HeaderBand.TipBorder + HeaderBand.TipPadY, null, null, "text");
            UiNode.Of(_tip).PointerEvents = PointerEvents.None;
            _tip.SetAsLastSibling();
            _tipOwner = owner;
        }

        private void HideTip(object owner)
        {
            if (owner != null && !ReferenceEquals(owner, _tipOwner)) return;
            if (_tip) Ui.Remove(_tip);
            _tip = null;
            _tipOwner = null;
        }

        // ---- a button -----------------------------------------------------------------------------------------

        private sealed class Button
        {
            private readonly HeaderBar _bar;
            private readonly HeaderButtonKind _kind;
            private readonly BoxGraphic _box;
            private readonly PathGraphic _icon;
            private readonly TextMeshProUGUI _word;
            private bool _hovered, _ring;
            private TableBox _at;

            public Button(HeaderBar bar, HeaderButtonKind kind)
            {
                _bar = bar;
                _kind = kind;
                Root = Ui.Div(bar._root, "ocbg-header-btn " + kind, 0f, HeaderBand.ButtonY, HeaderBand.ButtonMinW, HeaderBand.ButtonH);
                Node = UiNode.Of(Root);
                _box = BoxGraphic.On(Root, HeaderBand.Css.Button);
                ClickArea area = ClickArea.On(_box);
                area.Click += _ => Press();
                area.HoverChanged += on =>
                {
                    _hovered = on;
                    Restyle();
                    if (on && State.Shown && State.Enabled) _bar.ShowTip(this, _at, HeaderBand.TipOf(_bar._content.Screen, _kind));
                    else _bar.HideTip(this);
                };
                _icon = Icon(Root, kind.ToString(), HeaderBand.Icons.Of(kind), false, HeaderBand.ButtonIcon, DsTokens.Ink);
                _word = Label.Make(Root, "", bar._buttonS, 0f, HeaderBand.ButtonLabelTop, null, null, "word");
                Focus = new Focusable { Target = Root, Press = Press, Ring = on => { _ring = on; Restyle(); }, CanFocus = () => State.Pressable };
                SetWord(HeaderBand.WordOf(kind));
            }

            public RectTransform Root { get; }
            public UiNode Node { get; }
            public Focusable Focus { get; }
            public HeaderButtonState State { get; private set; } = HeaderButtonState.Ready;
            public float Width { get; private set; } = HeaderBand.ButtonMinW;

            public void SetWord(string word)
            {
                Label.SetText(_word, word ?? "");
                float lw = Ocb.TextWidth(word ?? "", _bar._buttonS);
                Width = HeaderBand.ButtonW(lw);
                Node.W = Width;
                Ui.Place(_icon.rectTransform, (Width - HeaderBand.ButtonIcon) / 2f, HeaderBand.ButtonIconTop);
                Ui.Place(_word.rectTransform, (Width - lw) / 2f, HeaderBand.ButtonLabelTop);
            }

            public void SetState(HeaderButtonState state)
            {
                State = state;
                Node.Opacity = state.Enabled ? 1f : HeaderBand.DisabledOpacity;
                if (!state.Shown || !state.Enabled) _bar.HideTip(this);
                Restyle();
            }

            public void Place(TableBox box)
            {
                _at = box;
                Node.Place(box.X, box.Y, box.W, box.H);
            }

            private void Restyle()
            {
                string css = HeaderBand.Css.Button + (State.Lit ? HeaderBand.Css.ButtonLit : "") +
                    (_hovered && State.Pressable ? HeaderBand.Css.ButtonHover : "") + (_ring ? FocusCss : "");
                _box.SetCss(css);
            }

            private void Press()
            {
                if (!State.Pressable || !Root) return;
                _bar.HideTip(this);
                try { _bar._actions.Of(_kind)?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                Microtasks.Drain();
            }
        }

        // ---- the table's chip back to the base ----------------------------------------------------------------

        private sealed class Chip
        {
            private readonly HeaderBar _bar;
            private readonly BoxGraphic _box;
            private bool _hovered, _ring;

            public Chip(HeaderBar bar, RectTransform parent)
            {
                _bar = bar;
                float tw = Ocb.TextWidth(HeaderBand.BackWord, bar._tagS);
                Width = HeaderBand.TagW(tw);
                Root = Ui.Div(parent, "ocbg-header-back", 0f, 0f, Width, HeaderBand.TagH);
                Node = UiNode.Of(Root);
                _box = BoxGraphic.On(Root, HeaderBand.Css.Back);
                ClickArea area = ClickArea.On(_box);
                area.Click += _ => Press();
                area.HoverChanged += on => { _hovered = on; Restyle(); };
                Label.Make(Root, HeaderBand.BackWord, bar._tagS, HeaderBand.TagBorder + HeaderBand.TagPadX, HeaderBand.TagBorder, null, null, "word");
                Focus = new Focusable { Target = Root, Press = Press, Ring = on => { _ring = on; Restyle(); }, CanFocus = () => Visible };
            }

            public RectTransform Root { get; }
            public UiNode Node { get; }
            public Focusable Focus { get; }
            public float Width { get; }

            public bool Visible
            {
                get => Node && Node.Visible;
                set { if (Node) Node.Visible = value; }
            }

            private void Restyle() =>
                _box.SetCss(HeaderBand.Css.Back + (_hovered ? HeaderBand.Css.BackHover : "") + (_ring ? FocusCss : ""));

            private void Press()
            {
                if (!Visible) return;
                try { _bar._actions.OnBack?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                Microtasks.Drain();
            }
        }
    }
}
