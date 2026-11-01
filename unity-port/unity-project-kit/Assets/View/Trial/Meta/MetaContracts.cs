// The ctx every meta mount takes (scenes/meta/index.js; app/main.js show / openOverlay / journalCtx fill them). Field
// names are the JS keys in PascalCase. Every ctx also carries what main.js show() and openOverlay() assign first:
// { stage, audio, settings } (main.js:66, :77, :453).
//
// The mounts keep the JS shape (app.md §5.5):
//   TitleScene.Mount(host, TitleCtx)          → IScene     (js: scenes/meta/title.js:15)
//   HallScene.Mount(host, HallCtx)            → IScene     (js: scenes/meta/hall.js:77)
//   DepartureScene.Mount(host, DepartureCtx)  → IScene     (js: scenes/meta/departure.js:103)
//   EndingScene.Mount(host, EndingCtx)        → IScene     (js: scenes/meta/ending.js:47)
//   SettingsOverlay.Mount(host, SettingsCtx)  → IOverlay   (js: scenes/meta/settings.js:24)
//   OwnedCards.Mount(host, OwnedCtx)          → IOverlay   (js: scenes/meta/owned.js:33)
//   JournalDrawer.Mount(host, JournalCtx)     → IOverlay   (js: scenes/meta/journal.js:169)
// A scene reads view-models and texts from the bridges and calls back through its ctx; it never navigates itself.
using System;
using System.Collections.Generic;
using Exploration.Bridge;
using InheritanceCore;
using Trial.Audio;
using Trial.Bridge;
using Trial.Kit;

namespace Trial.Meta
{
    /// <summary>What every meta ctx carries (main.js show(): Object.assign({ stage, audio, settings }, ctx)).</summary>
    public abstract class MetaCtx
    {
        /// <summary>ctx.stage: the 1920×1080 stage (its ClockDriver drives the scene clocks).</summary>
        public TrialStage Stage;

        /// <summary>ctx.audio: the game's audio (every call is guarded: MetaUtil.SoundOf).</summary>
        public ITrialAudio Audio;

        /// <summary>ctx.settings: the one shared settings object the scenes read live.</summary>
        public TrialSettings Settings;

        /// <summary>Copies stage, audio and settings from another ctx (the router's base object).</summary>
        public T With<T>(MetaCtx from) where T : MetaCtx
        {
            if (from != null)
            {
                Stage = from.Stage;
                Audio = from.Audio;
                Settings = from.Settings;
            }
            return (T)this;
        }
    }

    // js: app/main.js:217 (toTitle), scenes/meta/title.js:10
    /// <summary>
    /// mountTitle's ctx: { stage, audio, settings, hasSave, unreadable?, onNew(), onContinue(), onSettings() }.
    /// `Unreadable`: a save is stored but could not be read; つづきから says so and はじめから asks first.
    /// </summary>
    public sealed class TitleCtx : MetaCtx
    {
        /// <summary>A saved lineage to continue (つづきから is offered).</summary>
        public bool HasSave;

        /// <summary>A stored save that did not load (つづきから says 「記録を読み込めない」).</summary>
        public bool Unreadable;

        public Action OnNew;
        public Action OnContinue;

        /// <summary>Null: 設定 is not offered (it reads .4 and the menu skips it).</summary>
        public Action OnSettings;
    }

    // js: app/main.js:265 (toHall), scenes/meta/hall.js:77
    /// <summary>
    /// mountHall's ctx: { stage, audio, settings, lineage, seed?, onDepart(lifeStart | null), onSettings(), onJournal?(),
    /// onTitle?() }. The router passes no OnJournal (the hall's 手記 button stays hidden, as in the JS).
    /// </summary>
    public sealed class HallCtx : MetaCtx
    {
        /// <summary>The lineage the hall shows (read only: the hall never writes it, INHERITANCE.md R-12).</summary>
        public Lineage Lineage;

        /// <summary>ctx.seed: the run seed startLife gets when the hall starts the life itself (null: the lineage's own).</summary>
        public ulong? Seed;

        /// <summary>
        /// 出立: the LifeStart the hall made (Inheritance.StartLife), or null when the lineage already has a Current life
        /// (the router then starts it itself, main.js:267).
        /// </summary>
        public Action<LifeStart> OnDepart;

        public Action OnSettings;

        /// <summary>Null hides the 手記 button and the J key (the router passes none).</summary>
        public Action OnJournal;

        /// <summary>Null hides 「タイトルへ」.</summary>
        public Action OnTitle;
    }

    // js: app/main.js:277 (toDeparture), scenes/meta/departure.js:92
    /// <summary>
    /// mountDeparture's ctx: { stage, audio, settings, lifeStart, owned, savedDeck?, seed?, onSetOut(choice), onBack(),
    /// onSettings()?, onJournal()? }. With OnSettings, Esc opens the settings; without it Esc goes back.
    /// </summary>
    public sealed class DepartureCtx : MetaCtx
    {
        /// <summary>Inheritance.startLife's result: its DeckMin / DeckMax / ToolSlots / ConsumableSlots are the limits.</summary>
        public LifeStart LifeStart;

        /// <summary>
        /// The card ids the heir may build from. Null or empty: the forty every character owns (Expedition.InitialKinds);
        /// either way the prototype deck's kinds join them (DeckBuilder.WithPrototypeKinds). See <see cref="OwnsCatalogue"/>.
        /// </summary>
        public IReadOnlyList<string> Owned;

        /// <summary>JS `owned: null` (as opposed to a missing key): every card of the catalogue is owned.</summary>
        public bool OwnsCatalogue;

        /// <summary>DeckBuilder.Save string of the deck the last life set out with (the save's deckSave), or null.</summary>
        public string SavedDeck;

        /// <summary>ctx.seed: the 「ランダム」 seed when the LifeStart has none (js: lifeStart.Seed ?? ctx.seed ?? 1).</summary>
        public ulong? Seed;

        /// <summary>
        /// 出立する: the choice as the router's makeStart reads it (deck, battleToolIds, consumableIds, loadout, talent,
        /// deckSave; DepartureChoice.Of(departure, DepartureCarry.Carry(departure, limits), deck.Save())).
        /// </summary>
        public Action<DepartureChoice> OnSetOut;

        /// <summary>「← 継承の間へ」.</summary>
        public Action OnBack;

        /// <summary>Null: Esc goes back instead of opening the settings, and the gear is hidden.</summary>
        public Action OnSettings;

        /// <summary>Null hides the 手記 button and the J key.</summary>
        public Action OnJournal;
    }

    // js: app/main.js:499 (endLife), scenes/meta/ending.js:42
    /// <summary>
    /// mountEnding's ctx: { stage, audio, ending (Inheritance.endingView(lineage)), lineage? (the place's backdrop and
    /// the waiting-trace line), layer?, onHall() }. With Ending null, the view is Inheritance.EndingView(Lineage).
    /// </summary>
    public sealed class EndingCtx : MetaCtx
    {
        public EndingView Ending;
        public Lineage Lineage;

        /// <summary>The layer whose backdrop the card stands in (null: the last life's Layer, else 7).</summary>
        public int? Layer;

        public Action OnHall;
    }

    /// <summary>settings.js saveQuitKind values (MetaText.SaveQuitOf reads them).</summary>
    public static class SaveQuitKinds
    {
        public const string Life = "life";
        public const string Battle = "battle";
        public const string Lineage = "lineage";
    }

    // js: app/main.js:81 (openOverlay 'settings'), scenes/meta/settings.js:18
    /// <summary>
    /// mountSettings's ctx: { stage, audio, settings, onChange(next), onSaveQuit?(), canSaveQuit? (false hides 保存して閉じる),
    /// inBattle?, saveQuitKind? ('life' | 'battle' | 'lineage'; null = 'battle' with InBattle, else 'life'), onClose() }.
    /// OnChange gets the whole panel as it stands (speed, speedStep, shortFx, master, effects, ambience, muted, reverb).
    /// </summary>
    public sealed class SettingsCtx : MetaCtx
    {
        /// <summary>JS `ctx.canSaveQuit !== false`: true unless the router says false.</summary>
        public bool CanSaveQuit = true;

        /// <summary>One of <see cref="SaveQuitKinds"/>; null = by InBattle.</summary>
        public string SaveQuitKind;

        public bool InBattle;

        /// <summary>Null hides 「保存して閉じる」.</summary>
        public Action OnSaveQuit;

        /// <summary>Every change: the settings as the panel stands now (a fresh object; the router assigns it).</summary>
        public Action<TrialSettings> OnChange;

        public Action OnClose;
    }

    // js: app/main.js:95 (openOverlay 'cards'), scenes/meta/owned.js:27
    /// <summary>
    /// mountOwnedCards's ctx: { stage, audio, owned (card ids), book? (MasteryBook; none: every card at step 0), deck?
    /// (DeckBuilder | CardInstance[] | ids: the battle deck to mark), expedition? (owned, book and deck are read off it
    /// when not given), onClose() }. The router passes the running life's Expedition only.
    /// </summary>
    public sealed class OwnedCtx : MetaCtx
    {
        /// <summary>Card ids owned; null: the Expedition's Owned.</summary>
        public IReadOnlyList<string> Owned;

        /// <summary>The mastery record; null: the Expedition's Mastery (none at all: every card at step 0).</summary>
        public BattleCore.MasteryBook Book;

        /// <summary>
        /// The battle deck to mark, as the JS takes it: a Depiction.Bridge.DeckBuilder, an IEnumerable of
        /// BattleCore.CardInstance, or an IEnumerable of card ids; null: the Expedition's Deck.
        /// </summary>
        public object Deck;

        /// <summary>The running life (main.js passes live.raw).</summary>
        public Expedition Expedition;

        public Action OnClose;
    }

    // js: app/main.js:110 (journalCtx), scenes/meta/journal.js:158
    /// <summary>
    /// mountJournal's ctx: { stage, audio, mode: 'explore' | 'battle', book?, pages?, expedition?, enemies?, enemyIds?,
    /// disclosureOf?(enemyId), pageId?, memo?, scribbles?, onMemo?(text), onClose() } plus main.js journalCtx's lineage.
    /// <see cref="ToScript"/> hands the engine-free part to Trial.Script.JournalBookOf.BookOf.
    /// </summary>
    public sealed class JournalCtx : MetaCtx
    {
        /// <summary>Trial.Script.JournalCtx.Battle / Explore ("battle" / "explore").</summary>
        public string Mode;

        /// <summary>A book handed in (its pages are read first).</summary>
        public global::Journal.JournalBook Book;

        /// <summary>Pages handed in, when no book is.</summary>
        public IReadOnlyList<global::Journal.JournalPage> Pages;

        /// <summary>The running life: its book when none is handed in, its 開示度 and its rare pages.</summary>
        public Expedition Expedition;

        /// <summary>main.js journalCtx: the saved lineage (journal.js itself does not read it).</summary>
        public Lineage Lineage;

        /// <summary>The fight's enemies with their names (and rows).</summary>
        public IReadOnlyList<global::Trial.Script.JournalFoe> Enemies;

        /// <summary>The fight's enemies by id.</summary>
        public IReadOnlyList<string> EnemyIds;

        /// <summary>The 開示度 of an enemy (e.g. Expedition.DisclosureOf); null reads the expedition's.</summary>
        public Func<string, int> DisclosureOf;

        /// <summary>js: EnemyPages.Has (Trial.Bridge.EnemyPages.Has); null: no enemy has a page of its own.</summary>
        public Func<string, bool> HasEnemyPage;

        /// <summary>js: EnemyPages.Page(id, 開示度) (Trial.Bridge.EnemyPages.Page).</summary>
        public Func<string, int, global::Journal.JournalPage> EnemyPage;

        /// <summary>The page to open on (book.Find); null: the first that opens here.</summary>
        public string PageId;

        /// <summary>The player's own memo text (null: no memo; "" an empty one).</summary>
        public string Memo;

        /// <summary>The 走り書き handed down ({ Generation, Name, Text }).</summary>
        public IReadOnlyList<Scribble> Scribbles;

        /// <summary>Makes the メモ page a sheet the player writes on.</summary>
        public Action<string> OnMemo;

        public Action OnClose;

        /// <summary>The engine-free fields as Trial.Script's JournalCtx (what JournalBookOf.BookOf / EnemiesOf read).</summary>
        public global::Trial.Script.JournalCtx ToScript() => new global::Trial.Script.JournalCtx
        {
            Mode = Mode,
            Book = Book,
            Pages = Pages,
            Expedition = Expedition,
            Enemies = Enemies,
            EnemyIds = EnemyIds,
            DisclosureOf = DisclosureOf,
            HasEnemyPage = HasEnemyPage,
            EnemyPage = EnemyPage,
            PageId = PageId,
            Memo = Memo,
            Scribbles = Scribbles,
            OnMemo = OnMemo,
            OnClose = OnClose,
        };
    }
}
