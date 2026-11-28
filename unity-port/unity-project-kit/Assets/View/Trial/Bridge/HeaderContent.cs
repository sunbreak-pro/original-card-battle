// What the shared header band (#1074) shows on each of the four screens that carry it: the base, 「支度の卓」, the
// exploration and the battle. The screens hand the numbers in; which button is pressable or lit, the place's name and
// its tags, and every word are decided here, so the four screens cannot drift apart and `dotnet test` holds the rules
// (Trial.Bridge.Tests/HeaderContentTests). Trial.Header's HeaderBar draws what this says; where it stands is HeaderBand.
using System;
using System.Collections.Generic;
using Depiction;

namespace Trial.Bridge
{
    /// <summary>The screens that carry the header.</summary>
    public enum HeaderScreen
    {
        /// <summary>The base, 「降り口の前」 (GateScene).</summary>
        Base,

        /// <summary>「支度の卓」 (DepartureScene).</summary>
        Table,

        /// <summary>The exploration map.</summary>
        Explore,

        /// <summary>The battle.</summary>
        Battle,
    }

    /// <summary>The picture before the place's name.</summary>
    public enum HeaderPlaceArt
    {
        /// <summary>The way down's arch (the base and the table).</summary>
        Base,

        /// <summary>The stairs (a layer: the exploration and the battle).</summary>
        Layer,
    }

    /// <summary>The buttons on the right of the band, left to right.</summary>
    public enum HeaderButtonKind
    {
        /// <summary>The battle speed (only in battle), with the separator to its right.</summary>
        Speed,

        /// <summary>The owned cards, with the deck's count on its corner.</summary>
        OwnedCards,

        Journal,

        Settings,
    }

    /// <summary>
    /// What a Tab stop of the band is: the table's chip back, then the buttons left to right. A screen that names the
    /// stops (the exploration's key legend) reads this instead of the stops' order.
    /// </summary>
    public enum HeaderStop
    {
        /// <summary>The table's chip back to the base.</summary>
        Back,

        Speed,

        OwnedCards,

        Journal,

        Settings,
    }

    /// <summary>Whether a button stands, takes a press, and is lit (the screen it opens is the one showing).</summary>
    public readonly struct HeaderButtonState : IEquatable<HeaderButtonState>
    {
        public HeaderButtonState(bool shown, bool enabled, bool lit)
        {
            Shown = shown;
            Enabled = enabled;
            Lit = lit;
        }

        public bool Shown { get; }

        /// <summary>False: drawn at <see cref="HeaderBand.DisabledOpacity"/> and takes no press.</summary>
        public bool Enabled { get; }

        /// <summary>The wick's 2 px frame; a lit button takes no press, as its screen is already open.</summary>
        public bool Lit { get; }

        public bool Pressable => Shown && Enabled && !Lit;

        public static HeaderButtonState Hidden => new HeaderButtonState(false, false, false);

        public static HeaderButtonState Ready => new HeaderButtonState(true, true, false);

        public static HeaderButtonState Off => new HeaderButtonState(true, false, false);

        public static HeaderButtonState On => new HeaderButtonState(true, true, true);

        public bool Equals(HeaderButtonState o) => Shown == o.Shown && Enabled == o.Enabled && Lit == o.Lit;

        public override bool Equals(object obj) => obj is HeaderButtonState o && Equals(o);

        public override int GetHashCode() => (Shown ? 1 : 0) | (Enabled ? 2 : 0) | (Lit ? 4 : 0);

        public override string ToString() => !Shown ? "hidden" : Lit ? "lit" : Enabled ? "ready" : "off";
    }

    /// <summary>The collapse calendar as the band shows it.</summary>
    public sealed class HeaderCalendar
    {
        public int DaysLeft;

        /// <summary>A king is down and the days do not run: the number turns steel and <see cref="Note"/> shows beside it.</summary>
        public bool Frozen;

        /// <summary>The short note beside the days while <see cref="Frozen"/>.</summary>
        public string Note = "";

        /// <summary>The calendar; a frozen one without a note of its own takes <see cref="HeaderBand.FrozenNote"/>.</summary>
        public static HeaderCalendar Of(int daysLeft, bool frozen, string note = null) => new HeaderCalendar
        {
            DaysLeft = daysLeft,
            Frozen = frozen,
            Note = frozen ? (string.IsNullOrEmpty(note) ? HeaderBand.FrozenNote : note) : "",
        };

        /// <summary>What shows beside the days: the note while frozen, else "".</summary>
        public string ShownNote => Frozen ? Note ?? "" : "";

        /// <summary>
        /// The same days with <see cref="HeaderBand.FrozenNote"/> beside them. The battle's band takes it: with the turn
        /// tags and the speed it has no room for CalendarText.Note's 「竜王が戻るまで 41 日。暦は止まっている」, which
        /// runs under the buttons on the longest layer title (#1074).
        /// </summary>
        public HeaderCalendar Shortened() => Of(DaysLeft, Frozen);
    }

    /// <summary>
    /// A number with its words, drawn as three runs on one baseline: <see cref="Lead"/>, then <see cref="LeadGap"/> px,
    /// <see cref="Number"/> in the mono face, <see cref="TailGap"/> px, <see cref="Tail"/>. An empty run takes no gap.
    /// </summary>
    public readonly struct HeaderFigure
    {
        public HeaderFigure(string lead, float leadGap, string number, float tailGap, string tail)
        {
            Lead = lead ?? "";
            LeadGap = leadGap;
            Number = number ?? "";
            TailGap = tailGap;
            Tail = tail ?? "";
        }

        public string Lead { get; }
        public float LeadGap { get; }
        public string Number { get; }
        public float TailGap { get; }
        public string Tail { get; }

        /// <summary>The runs as one line (the gaps a space where the band shows one): for logs and tests.</summary>
        public string Text => Lead + (Lead.Length > 0 && LeadGap >= HeaderBand.SpaceW ? " " : "") + Number +
            (Tail.Length > 0 && TailGap >= HeaderBand.SpaceW ? " " : "") + Tail;
    }

    /// <summary>Everything one band shows. Built by the For* methods; the screen then changes it through HeaderBar's setters.</summary>
    public sealed class HeaderContent
    {
        public HeaderScreen Screen;

        public string PlaceName = "";

        public HeaderPlaceArt PlaceArt;

        /// <summary>The tags after the name (「支度の卓」, 「ターン 3」, 「連戦 2 / 3」), left to right.</summary>
        public IReadOnlyList<string> Tags = Array.Empty<string>();

        /// <summary>The chip back to the base, right of the tags (the table only).</summary>
        public bool BackChip;

        /// <summary>Null: the band leaves the calendar out and packs HP and stamina against the rule.</summary>
        public HeaderCalendar Calendar;

        public bool HasCalendar => Calendar != null;

        public int Hp, MaxHp, Stamina, MaxStamina;

        /// <summary>The deck's count on the owned cards' corner.</summary>
        public int DeckCount;

        /// <summary>The speed button's word (only in battle), from <see cref="HeaderBand.SpeedLabel"/>.</summary>
        public string SpeedText = "";

        public HeaderButtonState Speed, OwnedCards, Journal, Settings;

        public HeaderButtonState StateOf(HeaderButtonKind kind)
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

        public void SetState(HeaderButtonKind kind, HeaderButtonState state)
        {
            switch (kind)
            {
                case HeaderButtonKind.Speed: Speed = state; break;
                case HeaderButtonKind.OwnedCards: OwnedCards = state; break;
                case HeaderButtonKind.Journal: Journal = state; break;
                case HeaderButtonKind.Settings: Settings = state; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such header button.");
            }
        }

        /// <summary>The days as the band prints them (only read while <see cref="HasCalendar"/>).</summary>
        public HeaderFigure Days => HeaderBand.DaysFigure(Calendar?.DaysLeft ?? 0);

        public HeaderFigure HpFigure => HeaderBand.OutOf(Hp, MaxHp);

        public HeaderFigure StaminaFigure => HeaderBand.OutOf(Stamina, MaxStamina);

        public string DeckBadge => HeaderBand.Count(DeckCount);

        // ---- the four screens ----------------------------------------------------------------------------------

        /// <summary>
        /// The base. A descent starts at full strength, so HP and stamina show their maximums; the owned cards open the
        /// table.
        /// </summary>
        public static HeaderContent ForBase(HeaderCalendar calendar, int maxHp, int maxStamina, int deckCount) =>
            AtTheBase(HeaderScreen.Base, calendar, maxHp, maxStamina, deckCount);

        /// <summary>The table: the base's band with 「支度の卓」 and the chip back; the owned cards stay lit.</summary>
        public static HeaderContent ForTable(HeaderCalendar calendar, int maxHp, int maxStamina, int deckCount) =>
            AtTheBase(HeaderScreen.Table, calendar, maxHp, maxStamina, deckCount);

        /// <summary>The exploration: the layer's title (ExplorationScreen.LayerTitle) and the body as it stands.</summary>
        public static HeaderContent ForExplore(string layerTitle, HeaderCalendar calendar, int hp, int maxHp, int stamina, int maxStamina,
            int deckCount, bool canOpenCards, bool canOpenSettings = true)
        {
            HeaderContent c = Body(HeaderScreen.Explore, layerTitle, HeaderPlaceArt.Layer, calendar, hp, maxHp, stamina, maxStamina, deckCount);
            c.OwnedCards = OwnedCardsState(HeaderScreen.Explore, canOpenCards);
            c.Settings = canOpenSettings ? HeaderButtonState.Ready : HeaderButtonState.Off;
            return c;
        }

        /// <summary>
        /// The battle: the layer's title with the turn (and the fight of a run of fights from the second fight count up),
        /// the speed on the left of the buttons. The owned cards do not stand, nor their count (Koudai-san, 2026-11-28,
        /// #1074): the speed, the journal and the settings close up. The settings take a press only where the battle allows
        /// them. A frozen calendar always takes the short note (<see cref="HeaderCalendar.Shortened"/>).
        /// </summary>
        public static HeaderContent ForBattle(string layerTitle, int turn, int fightIndex, int fightCount, HeaderCalendar calendar,
            int hp, int maxHp, int stamina, int maxStamina, BattleSpeedStep speed, bool canOpenSettings = true)
        {
            HeaderContent c = Body(HeaderScreen.Battle, layerTitle, HeaderPlaceArt.Layer, calendar?.Shortened(), hp, maxHp, stamina, maxStamina, 0);
            c.Tags = HeaderBand.BattleTags(turn, fightIndex, fightCount);
            c.Speed = HeaderButtonState.Ready;
            c.SpeedText = HeaderBand.SpeedLabel(speed);
            c.OwnedCards = OwnedCardsState(HeaderScreen.Battle, true);
            c.Settings = canOpenSettings ? HeaderButtonState.Ready : HeaderButtonState.Off;
            return c;
        }

        /// <summary>
        /// The owned cards' button on a screen: the base opens the table; the table is that screen, so it stays lit; the
        /// exploration opens them while <paramref name="canOpen"/>; the battle does not show them.
        /// </summary>
        public static HeaderButtonState OwnedCardsState(HeaderScreen screen, bool canOpen)
        {
            switch (screen)
            {
                case HeaderScreen.Base: return HeaderButtonState.Ready;
                case HeaderScreen.Table: return HeaderButtonState.On;
                case HeaderScreen.Explore: return canOpen ? HeaderButtonState.Ready : HeaderButtonState.Off;
                case HeaderScreen.Battle: return HeaderButtonState.Hidden;
                default: throw new ArgumentOutOfRangeException(nameof(screen), screen, "No such header screen.");
            }
        }

        private static HeaderContent AtTheBase(HeaderScreen screen, HeaderCalendar calendar, int maxHp, int maxStamina, int deckCount)
        {
            HeaderContent c = Body(screen, HeaderBand.BaseName, HeaderPlaceArt.Base, calendar, maxHp, maxHp, maxStamina, maxStamina, deckCount);
            if (screen == HeaderScreen.Table)
            {
                c.Tags = new[] { HeaderBand.TableTag };
                c.BackChip = true;
            }
            c.OwnedCards = OwnedCardsState(screen, true);
            return c;
        }

        private static HeaderContent Body(HeaderScreen screen, string place, HeaderPlaceArt art, HeaderCalendar calendar,
            int hp, int maxHp, int stamina, int maxStamina, int deckCount) => new HeaderContent
            {
                Screen = screen,
                PlaceName = place ?? "",
                PlaceArt = art,
                Calendar = calendar,
                Hp = hp,
                MaxHp = maxHp,
                Stamina = stamina,
                MaxStamina = maxStamina,
                DeckCount = deckCount,
                Speed = HeaderButtonState.Hidden,
                OwnedCards = HeaderButtonState.Ready,
                Journal = HeaderButtonState.Ready,
                Settings = HeaderButtonState.Ready,
            };
    }
}
