// Where everything of the shared header band (#1074) stands, its words, its line icons and its CSS. The band is the
// exploration's 96 px band made common to the base, 「支度の卓」, the exploration and the battle, after the design
// canvas's 「共通ヘッダー」 (docs/mockups/2026-11-28-common-header/header.dc.html): on the left the place, a rule, the
// calendar, HP and stamina packed one after another; on the right the buttons (the battle's speed and a separator,
// the owned cards with the deck's count, the journal, the settings).
//
// It lives here, engine-free, so the four screens read one set of values and `dotnet test` holds them
// (Trial.Bridge.Tests/HeaderBandTests): the band is as tall as the exploration map's top (StrataLayout.StageTop), every
// text is 16 px or more, amber stays on the stamina and the deck's count, and a button's bubble opens below the band.
// The widths of the texts are the screen's to measure; every x derived from them is computed here.
using System;
using System.Collections.Generic;
using System.Globalization;
using Depiction;

namespace Trial.Bridge
{
    /// <summary>How wide the place (its picture, name, tags and chip) stands.</summary>
    public enum HeaderPlaceWidth
    {
        /// <summary>As wide as what it holds: the calendar, HP and stamina follow it, so they shift from screen to screen.</summary>
        Packed,

        /// <summary>At least <see cref="HeaderBand.FixedPlaceW"/>: the calendar, HP and stamina keep one place on every screen.</summary>
        Fixed,
    }

    /// <summary>The buttons' boxes on the right of the band, for the buttons that stand.</summary>
    public sealed class HeaderButtonRow
    {
        private readonly Dictionary<HeaderButtonKind, TableBox> _boxes = new Dictionary<HeaderButtonKind, TableBox>();

        internal void Add(HeaderButtonKind kind, TableBox box) => _boxes[kind] = box;

        public bool Has(HeaderButtonKind kind) => _boxes.ContainsKey(kind);

        public TableBox Box(HeaderButtonKind kind) =>
            _boxes.TryGetValue(kind, out TableBox b) ? b : throw new ArgumentException("The " + kind + " button does not stand in this row.", nameof(kind));

        /// <summary>The line between the speed and the owned cards (only with the speed).</summary>
        public TableBox? Separator { get; internal set; }

        /// <summary>The left edge of the row (the first button's).</summary>
        public float Left { get; internal set; }
    }

    /// <summary>Where the left side's groups stand: the place, the rule after it, the calendar, HP, stamina.</summary>
    public sealed class HeaderLeftRow
    {
        public float PlaceX, PlaceW;
        public TableBox Divider;

        /// <summary>False: the row holds no calendar, and HP stands where it would have.</summary>
        public bool HasCalendar;

        /// <summary>Only meaningful while <see cref="HasCalendar"/>.</summary>
        public float CalendarX;

        public float HpX, StaminaX;

        /// <summary>The right edge of the stamina group: the buttons should start after it.</summary>
        public float Right;
    }

    public static class HeaderBand
    {
        // ---- the band ------------------------------------------------------------------------------------------

        public const float StageW = 1920f;

        /// <summary>The band's height, its 1 px bottom rule included (border-box).</summary>
        public const float H = 96f;

        public const float BorderBottom = 1f;

        /// <summary>The content box the items are centred in.</summary>
        public const float InnerH = H - BorderBottom;

        public const float PadX = 24f;

        /// <summary>Between the left side's groups (the place, the rule, the calendar, HP, stamina).</summary>
        public const float Gap = 28f;

        public static readonly TableBox Band = new TableBox(0f, 0f, StageW, H);

        /// <summary>
        /// The one switch over the place's width (#1074: Koudai-san asked for the items packed to the left; if the shift
        /// from screen to screen shows in Unity, this turns to <see cref="HeaderPlaceWidth.Fixed"/>).
        /// </summary>
        public static readonly HeaderPlaceWidth PlaceWidth = HeaderPlaceWidth.Packed;

        /// <summary>
        /// The fixed place's width: room for the longest title (「第 7 層　爛れた世界樹の間」) with the battle's two tags.
        /// A wider place still takes its own width.
        /// </summary>
        public const float FixedPlaceW = 640f;

        /// <summary>The values every screen reads: one set for all four (the screen only picks what the band says).</summary>
        public static HeaderBandMetrics MetricsOf(HeaderScreen screen)
        {
            if (!Enum.IsDefined(typeof(HeaderScreen), screen)) throw new ArgumentOutOfRangeException(nameof(screen), screen, "No such header screen.");
            return HeaderBandMetrics.Shared;
        }

        // ---- the place: its picture, its name, its tags and the chip back ---------------------------------------

        public const float PlaceIcon = 28f, PlaceGap = 12f, NameLine = 36f;

        /// <summary>A tag: 16 px on a 26 px line, 10 px each side, a 1 px steel frame.</summary>
        public const float TagLine = 26f, TagPadX = 10f, TagBorder = 1f, TagH = TagLine + 2f * TagBorder;

        public static float TagW(float textW) => textW + 2f * (TagPadX + TagBorder);

        /// <summary>The place's runs from its left: the picture, the name, each tag, the chip back (0 when none). Their x.</summary>
        public static float[] PlaceXs(float nameW, IReadOnlyList<float> tagWs, float backW)
        {
            var widths = new List<float> { PlaceIcon, nameW };
            if (tagWs != null) widths.AddRange(tagWs);
            if (backW > 0f) widths.Add(backW);
            return DepartTable.RowFrom(0f, PlaceGap, widths.ToArray());
        }

        /// <summary>What the place holds, end to end.</summary>
        public static float PlaceContentW(float nameW, IReadOnlyList<float> tagWs, float backW)
        {
            float w = PlaceIcon + PlaceGap + nameW;
            if (tagWs != null)
                foreach (float t in tagWs) w += PlaceGap + t;
            if (backW > 0f) w += PlaceGap + backW;
            return w;
        }

        /// <summary>The place's width under a switch.</summary>
        public static float PlaceW(float contentW, HeaderPlaceWidth mode) => mode == HeaderPlaceWidth.Fixed ? Math.Max(FixedPlaceW, contentW) : contentW;

        // ---- the rule after the place ---------------------------------------------------------------------------

        public const float DividerW = 1f, DividerH = 40f;

        // ---- the calendar, HP and stamina --------------------------------------------------------------------------

        /// <summary>Between a figure's picture, its runs and the frozen note.</summary>
        public const float FigureGap = 10f;

        public const float CalendarIcon = 26f, HeartIcon = 22f, StaminaPip = 20f;

        /// <summary>The days' number stands 4 px off 「残り」 and 「日」.</summary>
        public const float DaysMargin = 4f;

        /// <summary>A space of the 20 px words: before 「/」 of 「38 / 50」.</summary>
        public const float SpaceW = 5f;

        /// <summary>A figure's runs from its left: the picture, the lead, the number, the tail, the note (0 when none). Their x.</summary>
        public static float[] FigureXs(float iconW, HeaderFigure f, float leadW, float numberW, float tailW, float noteW = 0f)
        {
            var xs = new float[5];
            float x = iconW + FigureGap;
            xs[0] = 0f;
            xs[1] = x;
            if (f.Lead.Length > 0) x += leadW + f.LeadGap;
            xs[2] = x;
            x += numberW;
            if (f.Tail.Length > 0) x += f.TailGap;
            xs[3] = x;
            if (f.Tail.Length > 0) x += tailW;
            xs[4] = noteW > 0f ? x + FigureGap : x;
            return xs;
        }

        /// <summary>A figure end to end.</summary>
        public static float FigureW(float iconW, HeaderFigure f, float leadW, float numberW, float tailW, float noteW = 0f)
        {
            float[] xs = FigureXs(iconW, f, leadW, numberW, tailW, noteW);
            return xs[4] + noteW;
        }

        /// <summary>
        /// The left side: the place from the band's padding, then the rule, the calendar, HP and stamina, 28 apart.
        /// Without <paramref name="withCalendar"/> the calendar takes no slot (and no gap): HP follows the rule.
        /// </summary>
        public static HeaderLeftRow LeftRow(float placeW, float calendarW, float hpW, float staminaW, bool withCalendar = true)
        {
            float[] xs = withCalendar
                ? DepartTable.RowFrom(PadX, Gap, placeW, DividerW, calendarW, hpW, staminaW)
                : DepartTable.RowFrom(PadX, Gap, placeW, DividerW, hpW, staminaW);
            int hp = withCalendar ? 3 : 2;
            return new HeaderLeftRow
            {
                PlaceX = xs[0],
                PlaceW = placeW,
                Divider = new TableBox(xs[1], (InnerH - DividerH) / 2f, DividerW, DividerH),
                HasCalendar = withCalendar,
                CalendarX = withCalendar ? xs[2] : xs[hp],
                HpX = xs[hp],
                StaminaX = xs[hp + 1],
                Right = xs[hp + 1] + staminaW,
            };
        }

        // ---- the buttons ------------------------------------------------------------------------------------------

        /// <summary>A button: 72 tall, 76 wide at least, padding 6 12 4, the picture (28) 4 over its word (16 px on 20).</summary>
        public const float ButtonH = 72f, ButtonMinW = 76f, ButtonPadTop = 6f, ButtonPadX = 12f, ButtonPadBottom = 4f, ButtonBorder = 1f,
            ButtonLitBorder = 2f, ButtonIcon = 28f, ButtonIconGap = 4f, ButtonLabelLine = 20f, ButtonGap = 8f;

        /// <summary>The buttons stand centred in the content box.</summary>
        public const float ButtonY = (InnerH - ButtonH) / 2f;

        /// <summary>A disabled button (the battle's owned cards, the exploration's while a card waits).</summary>
        public const float DisabledOpacity = 0.4f;

        public static float ButtonW(float labelW) => Math.Max(ButtonMinW, Math.Max(ButtonIcon, labelW) + 2f * (ButtonPadX + ButtonBorder));

        /// <summary>The picture's top and the word's top inside a button (the 52 px stack centred in the content box).</summary>
        public static float ButtonIconTop => ButtonBorder + ButtonPadTop + (ButtonH - 2f * ButtonBorder - ButtonPadTop - ButtonPadBottom - ButtonStackH) / 2f;

        public static float ButtonLabelTop => ButtonIconTop + ButtonIcon + ButtonIconGap;

        private const float ButtonStackH = ButtonIcon + ButtonIconGap + ButtonLabelLine;

        /// <summary>The line right of the speed: 48 tall, 8 px off each side beyond the buttons' gap.</summary>
        public const float SeparatorW = 1f, SeparatorH = 48f, SeparatorMargin = 8f;

        /// <summary>
        /// The buttons right-aligned to the band's padding, 8 apart: the speed (when its width is over 0) and the
        /// separator, the owned cards, the journal, the settings.
        /// </summary>
        public static HeaderButtonRow ButtonRow(float speedW, float ownedCardsW, float journalW, float settingsW)
        {
            var kinds = new List<HeaderButtonKind>();
            var widths = new List<float>();
            if (speedW > 0f)
            {
                kinds.Add(HeaderButtonKind.Speed);
                widths.Add(speedW);
                widths.Add(SeparatorMargin + SeparatorW + SeparatorMargin);
            }
            kinds.Add(HeaderButtonKind.OwnedCards);
            widths.Add(ownedCardsW);
            kinds.Add(HeaderButtonKind.Journal);
            widths.Add(journalW);
            kinds.Add(HeaderButtonKind.Settings);
            widths.Add(settingsW);
            float[] xs = DepartTable.RowTo(StageW - PadX, ButtonGap, widths.ToArray());
            var row = new HeaderButtonRow { Left = xs[0] };
            int at = 0;
            foreach (HeaderButtonKind kind in kinds)
            {
                row.Add(kind, new TableBox(xs[at], ButtonY, widths[at], ButtonH));
                at++;
                if (kind == HeaderButtonKind.Speed)
                {
                    row.Separator = new TableBox(xs[at] + SeparatorMargin, (InnerH - SeparatorH) / 2f, SeparatorW, SeparatorH);
                    at++;
                }
            }
            return row;
        }

        // ---- the deck's count on the owned cards' corner ---------------------------------------------------------

        /// <summary>The count's pill: 6 px past the button's right, 8 over its top, 30 × 24 at least, 6 px each side.</summary>
        public const float BadgeOutRight = 6f, BadgeOutTop = 8f, BadgeMinW = 30f, BadgeH = 24f, BadgePadX = 6f;

        public static TableBox BadgeBox(TableBox button, float textW)
        {
            float w = Math.Max(BadgeMinW, textW + 2f * BadgePadX);
            return new TableBox(button.Right + BadgeOutRight - w, button.Y - BadgeOutTop, w, BadgeH);
        }

        // ---- a button's bubble ------------------------------------------------------------------------------------

        /// <summary>The bubble opens 8 px under the band (never over it), right edge on the button's.</summary>
        public const float TipGap = 8f;

        /// <summary>The bubble's padding (8 12) and frame (1).</summary>
        public const float TipPadX = 12f, TipPadY = 8f, TipBorder = 1f, TipLine = 22f;

        public static TableBox TipBox(TableBox button, float textW)
        {
            float w = textW + 2f * (TipPadX + TipBorder), h = TipLine + 2f * (TipPadY + TipBorder);
            float x = Math.Max(PadX, Math.Min(button.Right, StageW - PadX) - w);
            return new TableBox(x, H + TipGap, w, h);
        }

        // ---- the words --------------------------------------------------------------------------------------------

        /// <summary>The base's and the table's place.</summary>
        public const string BaseName = "拠点";

        public const string TableTag = "支度の卓";

        /// <summary>The chip on the table that leads back to the base.</summary>
        public const string BackWord = "拠点へ戻る";

        public const string DaysLead = "残り";
        public const string DaysTail = "日";

        /// <summary>The note of a frozen calendar when the screen gives none of its own.</summary>
        public const string FrozenNote = "暦は止まっている";

        public const string OwnedCardsWord = "所持カード";
        public const string JournalWord = "手記";
        public const string SettingsWord = "設定";
        public const string SpeedWord = "速さ";

        /// <summary>What each button's bubble says: what it opens and its key, as the exploration's band named them.</summary>
        public static string TipOf(HeaderButtonKind kind)
        {
            switch (kind)
            {
                case HeaderButtonKind.Speed: return BattleSpeed.Tooltip;
                case HeaderButtonKind.OwnedCards: return global::Exploration.ExplorationText.CardsButton;
                case HeaderButtonKind.Journal: return global::Exploration.ExplorationText.JournalButton;
                case HeaderButtonKind.Settings: return global::Exploration.ExplorationText.SettingsButton;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such header button.");
            }
        }

        /// <summary>The word under each button's picture.</summary>
        public static string WordOf(HeaderButtonKind kind, string speedText = null)
        {
            switch (kind)
            {
                case HeaderButtonKind.Speed: return string.IsNullOrEmpty(speedText) ? SpeedWord : speedText;
                case HeaderButtonKind.OwnedCards: return OwnedCardsWord;
                case HeaderButtonKind.Journal: return JournalWord;
                case HeaderButtonKind.Settings: return SettingsWord;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such header button.");
            }
        }

        /// <summary>The Tab stop a button is.</summary>
        public static HeaderStop StopOf(HeaderButtonKind kind)
        {
            switch (kind)
            {
                case HeaderButtonKind.Speed: return HeaderStop.Speed;
                case HeaderButtonKind.OwnedCards: return HeaderStop.OwnedCards;
                case HeaderButtonKind.Journal: return HeaderStop.Journal;
                case HeaderButtonKind.Settings: return HeaderStop.Settings;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such header button.");
            }
        }

        /// <summary>What a key legend calls a Tab stop (the speed by its plain word: its label changes with the step).</summary>
        public static string StopWord(HeaderStop stop)
        {
            switch (stop)
            {
                case HeaderStop.Back: return BackWord;
                case HeaderStop.Speed: return SpeedWord;
                case HeaderStop.OwnedCards: return OwnedCardsWord;
                case HeaderStop.Journal: return JournalWord;
                case HeaderStop.Settings: return SettingsWord;
                default: throw new ArgumentOutOfRangeException(nameof(stop), stop, "No such header stop.");
            }
        }

        /// <summary>「速さ ×1.25」.</summary>
        public static string SpeedLabel(BattleSpeedStep step) => SpeedWord + " " + BattleSpeed.Label(step);

        /// <summary>「ターン 3」.</summary>
        public static string TurnTag(int turn) => "ターン " + N(turn);

        /// <summary>「連戦 2 / 3」.</summary>
        public static string FightTag(int index, int count) => "連戦 " + N(index) + " / " + N(count);

        /// <summary>The battle's tags: the turn, and the fight of a run once the run holds two fights or more.</summary>
        public static IReadOnlyList<string> BattleTags(int turn, int fightIndex, int fightCount) =>
            fightCount >= 2 ? new[] { TurnTag(turn), FightTag(fightIndex, fightCount) } : new[] { TurnTag(turn) };

        /// <summary>「残り 87 日」 as three runs.</summary>
        public static HeaderFigure DaysFigure(int daysLeft) => new HeaderFigure(DaysLead, DaysMargin, N(daysLeft), DaysMargin, DaysTail);

        /// <summary>「38 / 50」: the number, then 「/ 50」 a space after it.</summary>
        public static HeaderFigure OutOf(int value, int max) => new HeaderFigure("", 0f, N(value), SpaceW, "/ " + N(max));

        /// <summary>The deck's count.</summary>
        public static string Count(int n) => N(n);

        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

        // ---- the line icons (the canvas's, 24 × 24, stroked 2.2 round; the heart is filled) ------------------------

        public static class Icons
        {
            /// <summary>The base and the table: the way down's arch over the floor.</summary>
            public const string Base = "M5 20v-9a7 7 0 0 1 14 0v9M3 20h18";

            /// <summary>A layer: stairs going down.</summary>
            public const string Layer = "M4 20h4v-4h4v-4h4V8h4";

            public const string Hourglass = "M7 4h10M7 20h10M8 4c0 4 4 5 4 8s-4 4-4 8M16 4c0 4-4 5-4 8s4 4 4 8";

            /// <summary>HP: filled in crimson.</summary>
            public const string Heart = "M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z";

            /// <summary>The owned cards: a card (the canvas's rect 7,4 11 × 15, corners 1.6) and the one behind it.</summary>
            public const string OwnedCards = "M8.6 4h7.8a1.6 1.6 0 0 1 1.6 1.6v11.8a1.6 1.6 0 0 1-1.6 1.6H8.6A1.6 1.6 0 0 1 7 17.4V5.6A1.6 1.6 0 0 1 8.6 4zM5 8v11a1.6 1.6 0 0 0 1.6 1.6H14";

            public const string Journal = "M4 5c3-1 6-1 8 1 2-2 5-2 8-1v13c-3-1-6-1-8 1-2-2-5-2-8-1zM12 6v13";

            /// <summary>The settings: the canvas's circle (r 3) and its eight spokes.</summary>
            public const string Settings = "M15 12a3 3 0 1 1-6 0a3 3 0 1 1 6 0zM12 3v3M12 18v3M3 12h3M18 12h3M5.6 5.6l2.1 2.1M16.3 16.3l2.1 2.1M5.6 18.4l2.1-2.1M16.3 7.7l2.1-2.1";

            /// <summary>The speed: two chevrons.</summary>
            public const string Speed = "M5 6l6 6-6 6M13 6l6 6-6 6";

            public static string Of(HeaderButtonKind kind)
            {
                switch (kind)
                {
                    case HeaderButtonKind.Speed: return Speed;
                    case HeaderButtonKind.OwnedCards: return OwnedCards;
                    case HeaderButtonKind.Journal: return Journal;
                    case HeaderButtonKind.Settings: return Settings;
                    default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such header button.");
                }
            }

            public static string Of(HeaderPlaceArt art) => art == HeaderPlaceArt.Base ? Base : Layer;
        }

        // ---- the CSS of every part (the canvas's; the colours are DS tokens where the canvas used a token's value) -

        /// <summary>
        /// HeaderBandTests reads every text here: 16 px or more, and the body colour under 24 px except the count on
        /// the amber pill. Amber stays on the stamina pip and the deck's count.
        /// </summary>
        public static class Css
        {
            public const string Band = "background: var(--panel); border-bottom: 1px solid var(--rule); box-shadow: 0 6px 18px rgba(16,20,29,.6);";
            public const string PlaceName = "font: 700 28px/36px var(--font-display); letter-spacing: .04em; white-space: nowrap;";
            public const string Tag = "border: 1px solid var(--steel); border-radius: var(--radius-tag);";
            public const string TagText = "font: 700 16px/26px var(--font-body); white-space: nowrap;";
            public const string Back = "background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-tag);";
            public const string BackHover = "border-color: var(--ink);";
            public const string Divider = "background: var(--rule);";
            public const string Words = "font: 700 20px/28px var(--font-body); white-space: nowrap;";
            public const string Days = "font: 700 32px/32px var(--font-mono); white-space: nowrap;";
            public const string FrozenNote = "font: 400 16px/22px var(--font-body); white-space: nowrap;";
            public const string Number = "font: 700 24px/28px var(--font-mono); white-space: nowrap;";
            public const string StaminaPip = "background: var(--amber); border-radius: 50%;";
            public const string Button = "background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel);";
            public const string ButtonHover = "border-color: var(--ink);";
            public const string ButtonLit = "background: #1d2433; border: 2px solid var(--wick);";
            public const string ButtonLabel = "font: 400 16px/20px var(--font-body); white-space: nowrap;";
            public const string Separator = "background: rgba(157,166,186,.4);";
            public const string Badge = "background: var(--amber); border-radius: 12px;";
            public const string BadgeText = "font: 700 16px/24px var(--font-mono); color: var(--ground); white-space: nowrap;";
            public const string Tip = "background: var(--panel-solid); border: 1px solid var(--steel); border-radius: var(--radius-panel); box-shadow: var(--shadow-tip);";
            public const string TipText = "font: 400 16px/22px var(--font-body); white-space: nowrap;";
        }
    }

    /// <summary>The band's values as one object, so a test can see that every screen reads the same one.</summary>
    public sealed class HeaderBandMetrics
    {
        internal static readonly HeaderBandMetrics Shared = new HeaderBandMetrics();

        private HeaderBandMetrics() { }

        public TableBox Band => HeaderBand.Band;
        public float PadX => HeaderBand.PadX;
        public float Gap => HeaderBand.Gap;
        public float ButtonH => HeaderBand.ButtonH;
        public float ButtonY => HeaderBand.ButtonY;
        public float ButtonGap => HeaderBand.ButtonGap;
        public HeaderPlaceWidth PlaceWidth => HeaderBand.PlaceWidth;
    }
}
