// Save by replay, the recording half (port of the browser trial's src/app/recorder.js: recordExpedition, wrap,
// settleWhenDecided; the log itself, replayLife and trimUnfinished are LifeLog.cs's). The JS wraps the Expedition, its
// ExplorationSession, the EncounterBattle and each CoreBattleSource in a Proxy that logs every call but the read-only
// ones; C# has no Proxy, so each gets a facade with one method per state-changing call the game makes:
//
//   E  StartEncounter / Return / Abandon / Learn / Train / CloseResult / ShowTaken / CloseTaken / Inherit
//   S  Step / Jump / UseConsumable / Act / Escape / Toggle / OpenTraining / PutOffTraining
//   B  StartNext / Finish
//   C  AdvanceAuto / EndTurn / TryPlay / TakeFreeStep
//
// Each runs on the raw object (so the objects' own internal calls are never logged: Return → Session.ReportBattle, the
// session's Keeper.TakeChest / TakeSurvey, Learn → Take), is logged only once it returns (a call that throws changed
// nothing and must not replay), and then runs onCall(tag, method) (the app's PersistSoon). Reads go through to the raw
// objects and are never logged (the JS's READ_ONLY and getters); a facade's Raw is the object itself.
//
// The replay switch: ReplayTable.Standard holds the calls today's cores have; the Expedition's trial calls (Inherit,
// CloseResult, ShowTaken, CloseTaken) are added here (Recorder.Register / TrialTable), and Recorder.ReplayLife replays
// with them.
//
// Engine-free (no UnityEngine).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;

namespace Trial.Bridge
{
    /// <summary>The log and the after-call hook the facades of one life share.</summary>
    internal sealed class Recording
    {
        internal Recording(LifeLog log, Action<string, string> onCall)
        {
            Log = log ?? new LifeLog();
            OnCall = onCall;
        }

        internal LifeLog Log { get; }
        internal Action<string, string> OnCall { get; }

        // js: app/recorder.js:35
        /// <summary>A call returned: log it (JSON arguments, runs of argument-less calls counted up), then tell onCall.</summary>
        internal void Logged(string tag, string method, params object[] args)
        {
            Log.PushArgs(tag, method, args ?? Array.Empty<object>());
            OnCall?.Invoke(tag, method);
        }
    }

    public static class Recorder
    {
        /// <summary>
        /// Calls that only read, never logged (js: recorder.js:12 READ_ONLY, verbatim). The facades log exactly the calls
        /// in the file header; everything here goes through to the raw object.
        /// </summary>
        // js: app/recorder.js:12
        // A read-only list, not a HashSet behind the interface: Unity runs with domain reload off, so a static a caller could
        // cast and change would carry the change into the next play session.
        public static readonly IReadOnlyCollection<string> ReadOnly = Array.AsReadOnly(new[]
        {
            "Screen", "CanOpen", "DisclosureOf", "HandIn", "PreviewFor", "Inspect", "RefusalText", "HasPlayableCard",
            "toJSON", "equals", "constructor", "NextLaunch", "TryDescend", "MapOf", "SyncLife", "CanUseSlot", "EventAt", "CanTeleport", "TargetFor",
            "CanTakeFreeStep", "ShownGapGlyph",
        });

        /// <summary>The calls the facades log, as "Tag.Method" (app.md §2.4; rules.md §7.2).</summary>
        public static readonly IReadOnlyList<string> Logged = Array.AsReadOnly(new[]
        {
            "E.StartEncounter", "E.Return", "E.Abandon", "E.Learn", "E.Train", "E.CloseResult", "E.ShowTaken", "E.CloseTaken", "E.Inherit",
            "S.Step", "S.Jump", "S.UseConsumable", "S.Act", "S.Escape", "S.Toggle", "S.OpenTraining", "S.PutOffTraining",
            "B.StartNext", "B.Finish",
            "C.AdvanceAuto", "C.EndTurn", "C.TryPlay", "C.TakeFreeStep",
        });

        /// <summary>
        /// Wraps an Expedition for recording into <paramref name="log"/> (a new log when null; a replayed log goes on from
        /// where it stands). <paramref name="onCall"/>(tag, method) runs after each logged call (the app saves then, so a
        /// fight in progress is in the save after every move and every script event). The result's WrapSource records a
        /// source the replay started (ReplayResult.Open) into the same log.
        /// </summary>
        // js: app/recorder.js:67
        public static RecordedExpedition RecordExpedition(Expedition expedition, LifeLog log = null, Action<string, string> onCall = null)
        {
            if (expedition == null) throw new ArgumentNullException(nameof(expedition));
            return new RecordedExpedition(expedition, new Recording(log, onCall));
        }

        /// <summary>
        /// Replays a log onto a fresh Expedition (the raw one, nothing logged), fights in progress included, with the
        /// trial's table (<see cref="TrialTable"/>) unless one is given. Returns the log as replayed (a copy) and the fight
        /// left in progress — the raw EncounterBattle and CoreBattleSource — or null (LifeReplay.ReplayLife).
        /// </summary>
        // js: app/recorder.js:88
        public static ReplayResult ReplayLife(Expedition expedition, IReadOnlyList<LogEntry> log, ReplayTable table = null) =>
            LifeReplay.ReplayLife(expedition, log, table ?? TrialTable());

        /// <summary>Replays a log onto a fresh Expedition; returns the log as replayed (ReplayLife's Kept).</summary>
        // js: app/recorder.js:109
        public static LifeLog Replay(Expedition expedition, IReadOnlyList<LogEntry> log, ReplayTable table = null) =>
            ReplayLife(expedition, log, table).Kept;

        /// <summary>
        /// Drops everything from the last B.StartNext that has no B.Finish after it: the fight left unfinished starts over.
        /// Only for a fight that cannot go on; a reload keeps the fight (ReplayLife).
        /// </summary>
        // js: app/recorder.js:161
        public static LifeLog TrimUnfinished(IReadOnlyList<LogEntry> log) => LifeLog.TrimUnfinished(log);

        /// <summary>ReplayTable.Standard() with the Expedition's trial calls (<see cref="Register"/>): what the trial replays with.</summary>
        public static ReplayTable TrialTable() => Register(ReplayTable.Standard());

        /// <summary>
        /// Adds the Expedition's trial calls to <paramref name="table"/> (replacing entries of the same name) and returns
        /// it: E.Inherit (its argument the take as TakenLegacy.ToJson wrote it), E.CloseResult, E.ShowTaken (the card as
        /// LegacyReceipt.ToJson wrote it), E.CloseTaken. E.Take is not among them: the C#'s Take is private (the shipped
        /// game never logs it).
        /// </summary>
        public static ReplayTable Register(ReplayTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            table.Add("E", "Inherit", (c, a) => c.Expedition.Inherit(TakenOf(a)));
            table.Add("E", "CloseResult", (c, a) => c.Expedition.CloseResult());
            table.Add("E", "ShowTaken", (c, a) => c.Expedition.ShowTaken(ReceiptOf(a)));
            table.Add("E", "CloseTaken", (c, a) => c.Expedition.CloseTaken());
            return table;
        }

        /// <summary>
        /// A fight is over the moment the core decides it (the call that brings HP to 0 returns), well before the screen
        /// has played the fall and held the result card. Hand it to the encounter there and then (EncounterBattle.Finish,
        /// logged B.Finish) and let the app save (<paramref name="onSettled"/>), so a reload during the fall or the result
        /// card can neither revive a dead heir nor undo a won fight (tier1 R1-16, INHERITANCE.md R-21).
        ///
        /// Give the scene the result's Source: every call goes through to the recorded source, and after each a decided
        /// fight is finished once. Finish() finishes it if that has not happened yet (the scene's onFinished, an
        /// unattended fight) and is a no-op after. Calls the scene still makes after the decision (the rest of the script)
        /// are logged after B.Finish and replay the same way. A fight brought back already decided is finished at once.
        /// </summary>
        // js: app/recorder.js:126
        public static SettlingFight SettleWhenDecided(RecordedBattle battle, RecordedSource source, Action onSettled = null)
        {
            if (battle == null) throw new ArgumentNullException(nameof(battle));
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new SettlingFight(source, () => battle.Finish(source), onSettled);
        }

        /// <summary>
        /// <see cref="SettleWhenDecided(RecordedBattle, RecordedSource, Action)"/> on the raw objects (nothing is logged):
        /// a fight a replay brought back, driven outside a recorder.
        /// </summary>
        // js: app/recorder.js:126
        public static SettlingFight SettleWhenDecided(EncounterBattle battle, CoreBattleSource source, Action onSettled = null)
        {
            if (battle == null) throw new ArgumentNullException(nameof(battle));
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new SettlingFight(source, () => battle.Finish(source), onSettled);
        }

        private static TakenLegacy TakenOf(ReplayArgs a)
        {
            object json = a.Has(0) ? a[0] : null;
            if (json == null) return null;
            try
            {
                return TakenLegacy.FromJson(json);
            }
            catch (InheritanceRuleException e)
            {
                throw new ReplayException("replay: E.Inherit argument 0 is not a take (" + e.Message + ")");
            }
        }

        private static LegacyReceipt ReceiptOf(ReplayArgs a)
        {
            object json = a.Has(0) ? a[0] : null;
            if (json == null) return null;
            try
            {
                return LegacyReceipt.FromJson(json);
            }
            catch (ArgumentException e)
            {
                throw new ReplayException("replay: E.ShowTaken argument 0 is not a card (" + e.Message + ")");
            }
        }
    }

    /// <summary>The Expedition, recorded (tag E). Reads go through to <see cref="Raw"/>.</summary>
    public sealed class RecordedExpedition
    {
        private readonly Recording _rec;
        private RecordedBattle _battle;

        internal RecordedExpedition(Expedition raw, Recording rec)
        {
            Raw = raw;
            _rec = rec;
            Session = new RecordedSession(raw.Session, rec);
        }

        /// <summary>The Expedition itself (its calls are not logged).</summary>
        public Expedition Raw { get; }

        /// <summary>The log every call of this life is appended to.</summary>
        public LifeLog Log => _rec.Log;

        /// <summary>The session, recorded (tag S).</summary>
        public RecordedSession Session { get; }

        /// <summary>The encounter being fought, recorded (tag B), or null (js: the Battle property wraps Raw.Battle).</summary>
        public RecordedBattle Battle => WrapBattle(Raw.Battle);

        /// <summary>Records a source the replay started (ReplayResult.Open.Source) into this life's log (tag C); null stays null.</summary>
        // js: app/recorder.js:68
        public RecordedSource WrapSource(CoreBattleSource source) => source == null ? null : new RecordedSource(source, _rec);

        // js: app/recorder.js:73
        private RecordedBattle WrapBattle(EncounterBattle battle)
        {
            if (battle == null) return null;
            if (_battle == null || !ReferenceEquals(_battle.Raw, battle)) _battle = new RecordedBattle(battle, _rec);
            return _battle;
        }

        // ---- logged (E) ----

        /// <summary>Expedition.StartEncounter, logged; the encounter comes back recorded (null when no battle waits).</summary>
        public RecordedBattle StartEncounter()
        {
            EncounterBattle r = Raw.StartEncounter();
            _rec.Logged("E", "StartEncounter");
            return WrapBattle(r);
        }

        public BattleReturn Return()
        {
            BattleReturn r = Raw.Return();
            _rec.Logged("E", "Return");
            return r;
        }

        public void Abandon()
        {
            Raw.Abandon();
            _rec.Logged("E", "Abandon");
        }

        public bool Learn(string cardId)
        {
            bool r = Raw.Learn(cardId);
            _rec.Logged("E", "Learn", cardId);
            return r;
        }

        public bool Train(string cardId)
        {
            bool r = Raw.Train(cardId);
            _rec.Logged("E", "Train", cardId);
            return r;
        }

        public bool CloseResult()
        {
            bool r = Raw.CloseResult();
            _rec.Logged("E", "CloseResult");
            return r;
        }

        /// <summary>Expedition.ShowTaken, logged with the card as LegacyReceipt.ToJson writes it.</summary>
        public bool ShowTaken(LegacyReceipt card)
        {
            bool r = Raw.ShowTaken(card);
            _rec.Logged("E", "ShowTaken", card?.ToJson());
            return r;
        }

        public bool CloseTaken()
        {
            bool r = Raw.CloseTaken();
            _rec.Logged("E", "CloseTaken");
            return r;
        }

        /// <summary>
        /// Expedition.Inherit, logged with the take as TakenLegacy.ToJson writes it. The router writes the lineage's take
        /// (Inheritance.TakeTrace) and this entry in one save (INHERITANCE.md R-21).
        /// </summary>
        public InheritResult Inherit(TakenLegacy taken)
        {
            InheritResult r = Raw.Inherit(taken);
            _rec.Logged("E", "Inherit", taken?.ToJson());
            return r;
        }

        // ---- reads (not logged) ----

        public IReadOnlyList<CardInstance> Deck => Raw.Deck;
        public IReadOnlyCollection<string> Owned => Raw.Owned;
        public MasteryBook Mastery => Raw.Mastery;
        public Departure Tools => Raw.Tools;
        public IReadOnlyList<string> Offer => Raw.Offer;
        public IReadOnlyList<bool> OfferFits => Raw.OfferFits;
        public IReadOnlyList<string> Consumables => Raw.Consumables;
        public IReadOnlyList<string> WaitingConsumables => Raw.WaitingConsumables;
        public int Pages => Raw.Pages;
        public int FoundPages => Raw.FoundPages;
        public int InheritedPages => Raw.InheritedPages;
        public string InheritedTraceId => Raw.InheritedTraceId;
        public int? InheritedLayer => Raw.InheritedLayer;
        public int DeckMax => Raw.DeckMax;
        public bool Trial => Raw.Trial;
        public ResultCard PendingResult => Raw.PendingResult;
        public LegacyReceipt PendingTaken => Raw.PendingTaken;
        public IReadOnlyList<EventOutcome> Events => Raw.Events;
        public IReadOnlyDictionary<string, int> JournalLevels => Raw.JournalLevels;
        public LifeJournal Journal => Raw.Journal;
        public int DisclosureOf(string enemyId) => Raw.DisclosureOf(enemyId);
        public BattleHandIn HandIn() => Raw.HandIn();

        /// <summary>A read (the JS logs it when called through its Proxy, so its screens read it on the raw Expedition).</summary>
        public string EnemyName(string enemyId) => Raw.EnemyName(enemyId);
    }

    /// <summary>The Expedition's ExplorationSession, recorded (tag S). Reads go through to <see cref="Raw"/>.</summary>
    public sealed class RecordedSession
    {
        private readonly Recording _rec;

        internal RecordedSession(ExplorationSession raw, Recording rec)
        {
            Raw = raw;
            _rec = rec;
        }

        public ExplorationSession Raw { get; }

        // ---- logged (S) ----

        public bool Step(int nodeId, RestChoice choice = RestChoice.Rest)
        {
            bool r = Raw.Step(nodeId, choice);
            _rec.Logged("S", "Step", nodeId, choice);
            return r;
        }

        public bool Jump(int nodeId, RestChoice choice = RestChoice.Rest)
        {
            bool r = Raw.Jump(nodeId, choice);
            _rec.Logged("S", "Jump", nodeId, choice);
            return r;
        }

        public bool UseConsumable(int index)
        {
            bool r = Raw.UseConsumable(index);
            _rec.Logged("S", "UseConsumable", index);
            return r;
        }

        /// <summary>ExplorationSession.Act, logged as [action] or, with an argument, [action, arg].</summary>
        public bool Act(ScreenAction action, string arg = null)
        {
            bool r = Raw.Act(action, arg);
            if (arg == null) _rec.Logged("S", "Act", action);
            else _rec.Logged("S", "Act", action, arg);
            return r;
        }

        public bool Escape()
        {
            bool r = Raw.Escape();
            _rec.Logged("S", "Escape");
            return r;
        }

        public bool Toggle(Overlay overlay)
        {
            bool r = Raw.Toggle(overlay);
            _rec.Logged("S", "Toggle", overlay);
            return r;
        }

        public bool OpenTraining()
        {
            bool r = Raw.OpenTraining();
            _rec.Logged("S", "OpenTraining");
            return r;
        }

        public bool PutOffTraining()
        {
            bool r = Raw.PutOffTraining();
            _rec.Logged("S", "PutOffTraining");
            return r;
        }

        // ---- reads (not logged) ----

        // Every public read of ExplorationSession goes through (the JS Proxy hands every getter of the raw session on;
        // the explore scene reads PendingEvent, PendingFound, PendingEncounter, PendingBattleNode, CanUseConsumables …
        // through the recorded session). The Keeper is read only here: it is the Expedition's to set.
        public ExplorationState State => Raw.State;
        public bool Trial => Raw.Trial;
        public int Life => Raw.Life;
        public ulong RunSeed => Raw.RunSeed;
        public IEnemyIntel Intel => Raw.Intel;
        public IExplorationKeeper Keeper => Raw.Keeper;
        public bool CarvingTaken => Raw.CarvingTaken;
        public bool Surveyed => Raw.Surveyed;
        public int PendingBattleNode => Raw.PendingBattleNode;
        public Encounter PendingEncounter => Raw.PendingEncounter;
        public int PendingStepNode => Raw.PendingStepNode;
        public RestChoice PendingStepChoice => Raw.PendingStepChoice;
        public int InterludeHealed => Raw.InterludeHealed;
        public bool HasPendingBattle => Raw.HasPendingBattle;
        public bool HasPendingStep => Raw.HasPendingStep;
        public bool HasPendingEvent => Raw.HasPendingEvent;
        public bool HasPendingFound => Raw.HasPendingFound;
        public bool HasPendingSurvey => Raw.HasPendingSurvey;
        public bool ConfirmingDescend => Raw.ConfirmingDescend;
        public bool ConfirmingEndLife => Raw.ConfirmingEndLife;
        public bool IsIdle => Raw.IsIdle;
        public bool MapIsLive => Raw.MapIsLive;
        public bool CanEndLife => Raw.CanEndLife;
        public bool CanUseConsumables => Raw.CanUseConsumables;
        public bool IsAiming => Raw.IsAiming;
        public int AimingSlot => Raw.AimingSlot;
        public int PendingJumpSlot => Raw.PendingJumpSlot;
        public bool TrainingWaiting => Raw.TrainingWaiting;
        public Overlay OpenOverlay => Raw.OpenOverlay;
        public PendingEventInfo PendingEvent => Raw.PendingEvent;
        public IReadOnlyList<string> PendingFound => Raw.PendingFound;
        public IReadOnlyList<string> SurveyChoices => Raw.SurveyChoices;
        public string SurveyTarget => Raw.SurveyTarget;
        public bool SurveyPicked => Raw.SurveyPicked;
        public ExplorationState EventState => Raw.EventState;
        public ExplorationState InterludeState => Raw.InterludeState;
        public IReadOnlyList<EventOutcome> Events => Raw.Events;
        public ExplorationScreen Screen() => Raw.Screen();
        public bool CanOpen(Overlay overlay) => Raw.CanOpen(overlay);
        public bool CanUseSlot(int index) => Raw.CanUseSlot(index);
        public bool CanUseConsumable(int index) => Raw.CanUseConsumable(index);
        public bool HasSurveyed(int layer) => Raw.HasSurveyed(layer);
        public ExplorationState TryDescend() => Raw.TryDescend();
        public EventKind? EventAt(int nodeId) => Raw.EventAt(nodeId);
        public LayerMap MapOf(LayerProfile profile, ulong seed) => Raw.MapOf(profile, seed);
        public bool CarvingKnownAt(ulong seed) => Raw.CarvingKnownAt(seed);
    }

    /// <summary>The EncounterBattle, recorded (tag B). Reads go through to <see cref="Raw"/>.</summary>
    public sealed class RecordedBattle
    {
        private readonly Recording _rec;

        internal RecordedBattle(EncounterBattle raw, Recording rec)
        {
            Raw = raw;
            _rec = rec;
        }

        public EncounterBattle Raw { get; }

        // ---- logged (B) ----

        /// <summary>EncounterBattle.StartNext, logged with its argument; the source comes back recorded (tag C).</summary>
        // js: app/recorder.js:73 (children.calls.StartNext: wrapSource)
        public RecordedSource StartNext(bool suggestCards = false)
        {
            CoreBattleSource r = Raw.StartNext(suggestCards);
            _rec.Logged("B", "StartNext", suggestCards);
            return new RecordedSource(r, _rec);
        }

        /// <summary>EncounterBattle.Finish on the recorded source, logged as ["$source"].</summary>
        public void Finish(RecordedSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Raw.Finish(source.Raw);
            _rec.Logged("B", "Finish", LifeLog.SourceMarker);
        }

        /// <summary>
        /// The C#'s EncounterBattle.TryFinish (no JS counterpart): finishes the fight when the core decided it, logged as
        /// the B.Finish it is; nothing is logged when it returns false (nothing changed).
        /// </summary>
        public bool TryFinish(RecordedSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            bool r = Raw.TryFinish(source.Raw);
            if (r) _rec.Logged("B", "Finish", LifeLog.SourceMarker);
            return r;
        }

        // ---- reads (not logged) ----

        public BattleHandIn HandIn => Raw.HandIn;
        public IReadOnlyList<FoughtBattle> Fought => Raw.Fought;
        public int Index => Raw.Index;
        public int Count => Raw.Count;
        public bool Over => Raw.Over;
        public bool Lost => Raw.Lost;
        public bool Won => Raw.Won;
        public int HpAfter => Raw.HpAfter;
        public int StaminaAfter => Raw.StaminaAfter;
        public BattleLaunch NextLaunch() => Raw.NextLaunch();
    }

    /// <summary>
    /// A CoreBattleSource, recorded (tag C): the battle scene's source (ITrialSource). AdvanceAuto, EndTurn, TryPlay and
    /// TakeFreeStep are logged; every other member reads through to <see cref="Raw"/>.
    /// </summary>
    public sealed class RecordedSource : ITrialSource
    {
        private readonly Recording _rec;

        internal RecordedSource(CoreBattleSource raw, Recording rec)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            _rec = rec;
        }

        public CoreBattleSource Raw { get; }

        // ---- logged (C) ----

        public DepictionEvent AdvanceAuto()
        {
            DepictionEvent r = Raw.AdvanceAuto();
            _rec.Logged("C", "AdvanceAuto");
            return r;
        }

        public DepictionEvent EndTurn()
        {
            DepictionEvent r = Raw.EndTurn();
            _rec.Logged("C", "EndTurn");
            return r;
        }

        /// <summary>CoreBattleSource.TryPlay, logged as [card, zone] (a refused release too: it replays to the same refusal).</summary>
        public PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played)
        {
            PlayVerdict r = Raw.TryPlay(cardId, zone, out played);
            _rec.Logged("C", "TryPlay", cardId, zone);
            return r;
        }

        /// <summary>CoreBattleSource.TryPlay on a named enemy, logged as [card, zone, target] (player.js:1677).</summary>
        public PlayVerdict TryPlay(string cardId, DropZone zone, int target, out DepictionEvent played)
        {
            PlayVerdict r = Raw.TryPlay(cardId, zone, target, out played);
            _rec.Logged("C", "TryPlay", cardId, zone, target);
            return r;
        }

        public DepictionEvent TakeFreeStep(int dir)
        {
            DepictionEvent r = Raw.TakeFreeStep(dir);
            _rec.Logged("C", "TakeFreeStep", dir);
            return r;
        }

        // ---- reads (not logged) ----

        public DepictionFrame Frame => Raw.Frame;
        public bool Finished => Raw.Finished;
        public bool WaitingForPlayer => Raw.WaitingForPlayer;
        public bool CanEndTurn => Raw.CanEndTurn;
        public string SuggestedCardId => Raw.SuggestedCardId;
        public string GuideText => Raw.GuideText;
        public BattleState State => Raw.State;
        public IReadOnlyList<BattleEvent> History => Raw.History;
        public string AutoCardId => Raw.AutoCardId;
        public int TargetFor(string cardId) => Raw.TargetFor(cardId);
        public PlayVerdict Inspect(string cardId) => Raw.Inspect(cardId);
        public PlayVerdict Inspect(string cardId, int target) => Raw.Inspect(cardId, target);
        public string PreviewFor(string cardId) => Raw.PreviewFor(cardId);
        public string PreviewFor(string cardId, int target) => Raw.PreviewFor(cardId, target);
        public string RefusalText(string cardId, PlayVerdict verdict) => Raw.RefusalText(cardId, verdict);
        public string RefusalText(string cardId, PlayVerdict verdict, int target) => Raw.RefusalText(cardId, verdict, target);
        public bool CanTakeFreeStep(int? dir = null) => Raw.CanTakeFreeStep(dir);
    }

    /// <summary>
    /// settleWhenDecided's result (js: recorder.js:153 <c>{ source, finish(), settled }</c>): the source to give the
    /// scene, the finish the scene's end calls, and whether the fight was finished.
    /// </summary>
    public sealed class SettlingFight
    {
        private readonly ITrialSource _inner;
        private readonly Action _finish;
        private readonly Action _onSettled;
        private bool _done;

        internal SettlingFight(ITrialSource inner, Action finish, Action onSettled)
        {
            _inner = inner;
            _finish = finish;
            _onSettled = onSettled;
            Source = new SettlingSource(this);
            // A fight brought back by a reload that the core had already decided (its Finish never reached the save) is
            // finished at once.
            if (Decided()) Finish();
        }

        /// <summary>The source to give the scene: every call goes through, and after each method a decided fight is finished once.</summary>
        public ITrialSource Source { get; }

        /// <summary>The fight was finished (at its decision, or by <see cref="Finish"/>).</summary>
        public bool Settled => _done;

        // js: app/recorder.js:132
        /// <summary>Finishes the fight (EncounterBattle.Finish, then onSettled) unless that happened already. Returns whether it did now.</summary>
        public bool Finish()
        {
            if (_done) return false;
            _done = true;
            _finish();
            _onSettled?.Invoke();
            return true;
        }

        // js: app/recorder.js:128
        private bool Decided()
        {
            BattleState st = _inner.State;
            return st != null && st.Result != GameResult.Ongoing;
        }

        // js: app/recorder.js:143 — after every call on the view.
        private void After()
        {
            if (!_done && Decided()) Finish();
        }

        /// <summary>The JS view Proxy: methods are followed by the check, properties read through.</summary>
        private sealed class SettlingSource : ITrialSource
        {
            private readonly SettlingFight _fight;

            internal SettlingSource(SettlingFight fight) { _fight = fight; }

            private ITrialSource Inner => _fight._inner;

            private T Then<T>(T result)
            {
                _fight.After();
                return result;
            }

            public DepictionFrame Frame => Inner.Frame;
            public bool Finished => Inner.Finished;
            public bool WaitingForPlayer => Inner.WaitingForPlayer;
            public bool CanEndTurn => Inner.CanEndTurn;
            public string SuggestedCardId => Inner.SuggestedCardId;
            public string GuideText => Inner.GuideText;
            public BattleState State => Inner.State;
            public IReadOnlyList<BattleEvent> History => Inner.History;
            public string AutoCardId => Inner.AutoCardId;

            public DepictionEvent AdvanceAuto() => Then(Inner.AdvanceAuto());
            public DepictionEvent EndTurn() => Then(Inner.EndTurn());
            public PlayVerdict Inspect(string cardId) => Then(Inner.Inspect(cardId));
            public PlayVerdict Inspect(string cardId, int target) => Then(Inner.Inspect(cardId, target));

            public PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played)
            {
                PlayVerdict r = Inner.TryPlay(cardId, zone, out played);
                _fight.After();
                return r;
            }

            public PlayVerdict TryPlay(string cardId, DropZone zone, int target, out DepictionEvent played)
            {
                PlayVerdict r = Inner.TryPlay(cardId, zone, target, out played);
                _fight.After();
                return r;
            }

            public string PreviewFor(string cardId) => Then(Inner.PreviewFor(cardId));
            public string PreviewFor(string cardId, int target) => Then(Inner.PreviewFor(cardId, target));
            public string RefusalText(string cardId, PlayVerdict verdict) => Then(Inner.RefusalText(cardId, verdict));
            public string RefusalText(string cardId, PlayVerdict verdict, int target) => Then(Inner.RefusalText(cardId, verdict, target));
            public int TargetFor(string cardId) => Then(Inner.TargetFor(cardId));
            public bool CanTakeFreeStep(int? dir = null) => Then(Inner.CanTakeFreeStep(dir));
            public DepictionEvent TakeFreeStep(int dir) => Then(Inner.TakeFreeStep(dir));
        }
    }
}
