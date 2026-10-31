// The game shell (port of src/app/main.js): one stage, one audio, the settings, the lineage, and a router between
// scenes. Scenes never navigate; they call back here. Rules stay in the ported cores and the inheritance module; the
// save's data half (stamps, persist, stale) is Trial.Bridge.SaveKeeper's, the life's data half (lifeSetup, replay,
// record) is Trial.Bridge.Life's. This file keeps main.js's functions, in its order, with their JS names in PascalCase.
//
// Made by TrialBoot (the scene component), which builds the stage, the audio, the settings and the save keeper first
// (main.js:19-35), calls Start (main.js:512) and pumps RunTasks (setTimeout) and the save flush every frame.
//
// Layering on the stage (CSS z-index → TrialStage layer order): scene layers (auto → 0) < the overlay host (500) <
// stage notices (.ocbg-notice 880) < confirm cards (.ocbg-confirm 900). Notices and confirm cards are not scene layers,
// so they survive Show (resumeLife's failure card is asked over the new title).
//
// Async: JS promises are Trial.Promise (await new Promise(resolve => show(battle, { onFinished: resolve })) is a
// PromiseSource; confirmCard().then is Promise<bool>.Then). setTimeout(fn, 0) is PostTask (run by RunTasks, the next
// frame). The browser's `storage` event has no counterpart: the stamp check before every write (SaveKeeper.InSync)
// and SyncIdle on the title's clicks keep a second instance from writing over a newer save (app.md §2.8).
using System;
using System.Collections.Generic;
using System.Linq;
using Depiction;
using Depiction.Bridge;
using DungeonCore;
using Exploration.Bridge;
using InheritanceCore;
using Trial.Audio;
using Trial.Battle;
using Trial.Bridge;
using Trial.Explore;
using Trial.Kit;
using Trial.Meta;
using UnityEngine;
using MetaJournalCtx = Trial.Meta.JournalCtx;
using Saves = Trial.Bridge.Save;

namespace Trial.Boot
{
    /// <summary>
    /// openOverlay's `extra` (main.js:73): what overrides the settings panel's ctx after the router's own keys
    /// (Object.assign order). LINEAGE_SAVE in 継承の間 and 出立, { inBattle: true } in a fight.
    /// </summary>
    public sealed class OverlayExtra
    {
        public OverlayExtra(bool? canSaveQuit = null, string saveQuitKind = null, bool? inBattle = null)
        {
            CanSaveQuit = canSaveQuit;
            SaveQuitKind = saveQuitKind;
            InBattle = inBattle;
        }

        /// <summary>canSaveQuit (null: the router's `!!live`).</summary>
        public bool? CanSaveQuit { get; }

        /// <summary>saveQuitKind ('life' | 'battle' | 'lineage'; null: by InBattle).</summary>
        public string SaveQuitKind { get; }

        /// <summary>inBattle (null: false).</summary>
        public bool? InBattle { get; }

        // js: app/main.js:71
        /// <summary>設定・セーブ in 継承の間 and 出立: no life is in progress, but the lineage is saved (settings.js 'lineage').</summary>
        public static readonly OverlayExtra LineageSave = new OverlayExtra(canSaveQuit: true, saveQuitKind: SaveQuitKinds.Lineage);

        // js: app/main.js:457
        /// <summary>Esc and the gear in a fight: 「保存して閉じる」 (settings.js 'battle').</summary>
        public static readonly OverlayExtra Battle = new OverlayExtra(inBattle: true);
    }

    /// <summary>What Main is made with (TrialBoot's Inspector switches and the pieces main.js:19-35 builds).</summary>
    public sealed class MainOptions
    {
        public TrialStage Stage;
        public ITrialAudio Audio;
        public TrialSettings Settings;
        public IKeyValueStore Store;
        public SaveKeeper Keeper;

        /// <summary>?autobattle: every fight fought unattended with the source's AutoCardId (no battle scene).</summary>
        public bool Autobattle;

        /// <summary>はじめから's lineage seed: 0 = the JS's (Date.now() ^ random) >>> 0, else this one.</summary>
        public ulong LineageSeed;
    }

    public sealed class Main
    {
        // ------------------------------------------------------------------ main.js:19-51 the shell's state

        private readonly TrialStage _stage;
        private readonly ITrialAudio _audio;
        private readonly TrialSettings _settings;
        private readonly IKeyValueStore _store;
        private readonly SaveKeeper _keeper;
        private readonly bool _autobattle;
        private readonly ulong _lineageSeed;
        private readonly System.Random _random = new System.Random();

        // Stage-level layers (not .ocbg-scene: Show leaves them): notices (z 880) and confirm cards (z 900).
        private readonly RectTransform _noticeLayer;
        private readonly RectTransform _confirmLayer;

        private string _screen = "";                 // which screen is up: title / hall / departure / explore / battle / ending
        private IScene _scene;                       // the mounted scene { destroy }
        private IOverlay _overlay;                   // the mounted overlay { destroy, host }
        private RectTransform _overlayHost;
        private RectTransform _sceneHost;            // the mounted scene's host element
        private readonly List<RectTransform> _sceneLayers = new List<RectTransform>();   // every .ocbg-scene of the stage root
        private LiveLife _live;                      // { expedition (recorded), raw, log, start, life, memo, scribbles }
        private ITrialSource _fightNow;              // the source of the fight on screen (debug only)
        private bool _failing;
        private bool _destroyed;

        // The app's own notices inside a scene (the title's: .ocbg-app-notice) and the ones on the stage root.
        private readonly List<NoticeView> _appNotices = new List<NoticeView>();
        private readonly List<NoticeView> _stageNotices = new List<NoticeView>();

        // setTimeout(fn, 0): run by RunTasks at the next frame.
        private readonly List<Action> _tasks = new List<Action>();

        // js: app/main.js:423
        /// <summary>The ambience's level under a battle (Audio.duckAmbience takes a gain 0..1; 1 is no ducking).</summary>
        public const double BattleDuck = 0.4;

        public Main(MainOptions o)
        {
            if (o == null) throw new ArgumentNullException(nameof(o));
            _stage = o.Stage ? o.Stage : throw new ArgumentNullException(nameof(o.Stage));
            _audio = o.Audio;
            _settings = o.Settings ?? TrialSettings.Defaults();
            _store = o.Store ?? throw new ArgumentNullException(nameof(o.Store));
            _keeper = o.Keeper ?? SaveKeeper.Open(_store);
            _autobattle = o.Autobattle;
            _lineageSeed = o.LineageSeed;
            _noticeLayer = _stage.Layer("ocbg-notices", 880, raycasts: false);
            _confirmLayer = _stage.Layer("ocbg-confirms", 900, raycasts: true);
            _keeper.BecameStale += OnBecameStale;
            _keeper.SaveFailed += OnSaveFailed;
        }

        // ---- the state, as main.js keeps it (the save's data lives in the keeper) ----

        /// <summary>main.js `save`: { lineage, life, deckSave }, or null.</summary>
        public TrialSave Save
        {
            get => _keeper.Save;
            private set => _keeper.Save = value;
        }

        /// <summary>main.js `unreadable`: a stored save that did not load ({ error, kept }), or null.</summary>
        public Unreadable Unreadable
        {
            get => _keeper.Unreadable;
            private set => _keeper.Unreadable = value;
        }

        /// <summary>main.js `stale`: another instance wrote after this one.</summary>
        public bool Stale => _keeper.Stale;

        /// <summary>main.js `storageOk`.</summary>
        public bool StorageOk => _keeper.StorageOk;

        /// <summary>main.js `live`: the life in progress, or null (the keeper composes save.life from it).</summary>
        public LiveLife Live
        {
            get => _live;
            private set
            {
                _live = value;
                _keeper.Live = value;
            }
        }

        /// <summary>main.js `screen`.</summary>
        public string ScreenName => _screen;

        public IScene Scene => _scene;
        public IOverlay Overlay => _overlay;
        public SaveKeeper Keeper => _keeper;

        /// <summary>main.js `fightNow` (window.__ocb.fight).</summary>
        public ITrialSource FightNow => _fightNow;

        /// <summary>The stage layer the router's confirm cards are asked on (stage.root in the JS).</summary>
        public RectTransform ConfirmHost => _confirmLayer;

        // ------------------------------------------------------------------ main.js:53-119 the scene host, overlays

        // js: app/main.js:56
        /// <summary>
        /// The app's own notices inside a scene (the title's): they step back while an overlay is open, so the panel's
        /// notice is the only one (and nothing sits across the panel's frame).
        /// </summary>
        private void SceneNotices(bool visible)
        {
            _appNotices.RemoveAll(n => n == null || !n.Root);
            foreach (NoticeView n in _appNotices) n.Node.Visible = visible;
        }

        // js: app/main.js:60
        /// <summary>
        /// show(mount, ctx, cls): the overlay closes, the old scene is destroyed and every scene layer removed, then the
        /// new scene is mounted into a fresh layer. `mount` builds the scene's ctx itself (stage, audio and settings first,
        /// as Object.assign({ stage, audio, settings }, ctx) does). The outgoing scene is not refreshed by the overlay's
        /// close (main.js does; it is about to go, and a refresh here would drain its continuations first).
        /// </summary>
        private IScene Show(Func<RectTransform, IScene> mount, string cls)
        {
            CloseOverlay(refresh: false);
            IScene old = _scene;
            _scene = null;
            if (old != null)
            {
                try { old.Destroy(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            foreach (RectTransform l in _sceneLayers) RemoveLayer(l);
            _sceneLayers.Clear();
            _appNotices.Clear();
            RectTransform host = _stage.Layer("ocbg-scene " + (cls ?? ""), 0);
            _sceneLayers.Add(host);
            _sceneHost = host;
            _scene = mount(host);
            return _scene;
        }

        // js: app/main.js:73
        /// <summary>
        /// openOverlay(kind, extra): one overlay at a time ('settings' | 'cards' | 'journal') in a host over the scene
        /// (z 500). The title's notices step back while it is open.
        /// </summary>
        public void OpenOverlay(string kind, OverlayExtra extra = null)
        {
            CloseOverlay();
            RectTransform host = _stage.Layer("ocbg-scene ocbg-overlay-host", 500);
            SceneNotices(false);
            Expedition ex = Live?.Raw;
            IOverlay overlay = null;
            if (kind == ExploreOverlayKind.Settings)
            {
                overlay = SettingsOverlay.Mount(host, Base(new SettingsCtx
                {
                    OnClose = () => CloseOverlay(),
                    CanSaveQuit = extra?.CanSaveQuit ?? Live != null,
                    OnSaveQuit = SaveQuit,
                    OnChange = s =>
                    {
                        if (s != null) _settings.Assign(s);
                        Saves.WriteSettings(_store, _settings);
                        _audio?.ApplySettings(_settings.Master, _settings.Effects, _settings.Ambience, _settings.Reverb, _settings.Muted);
                        _settings.NotifyChanged();
                    },
                    SaveQuitKind = extra?.SaveQuitKind,
                    InBattle = extra?.InBattle ?? false,
                }));
                if (!StorageOk && overlay != null)
                {
                    // Over the panel: 「保存して閉じる」 must not look as if it kept anything. (It says what a passing
                    // 「保存できませんでした」 said, so that one goes.)
                    foreach (NoticeView n in _stageNotices) n?.Remove();
                    _stageNotices.Clear();
                    RectTransform panel = FindByClass(host, "ocbg-meta-settings-panel");
                    int y = panel ? AppText.SettingsNoticeY((int)Math.Round(OffsetTopIn(panel, host))) : AppText.SettingsNoticeFallbackY;
                    Stage.NoticeLine(host, string.Concat(AppText.NoStorageLines), AppText.SettingsNoticeX, y, AppText.SettingsNoticeWidth);
                }
            }
            else if (kind == ExploreOverlayKind.Cards)
            {
                overlay = OwnedCards.Mount(host, Base(new OwnedCtx
                {
                    OnClose = () => CloseOverlay(),
                    Expedition = ex,
                }));
            }
            else if (kind == ExploreOverlayKind.Journal)
            {
                MetaJournalCtx ctx = JournalCtx("explore");
                ctx.OnClose = () => CloseOverlay();
                overlay = JournalDrawer.Mount(host, ctx);
            }
            if (overlay != null)
            {
                _overlay = overlay;
                _overlayHost = host;
            }
            else
            {
                RemoveLayer(host);
                SceneNotices(true);
            }
        }

        // js: app/main.js:101
        /// <summary>closeOverlay(): the overlay and its host go, the title's notices come back, the scene repaints (refresh).</summary>
        public void CloseOverlay() => CloseOverlay(refresh: true);

        private void CloseOverlay(bool refresh)
        {
            if (_overlay == null) return;
            IOverlay overlay = _overlay;
            RectTransform host = _overlayHost;
            _overlay = null;   // before destroy: an overlay whose destroy calls onClose does not come back here
            _overlayHost = null;
            try { overlay.Destroy(); }
            catch (Exception e) { Debug.LogException(e); }
            RemoveLayer(host);
            SceneNotices(true);
            if (refresh) _scene?.Refresh();
        }

        // js: app/main.js:110
        /// <summary>journalCtx(mode): the running life's journal (its expedition, memo and 走り書き) and the saved lineage.</summary>
        private MetaJournalCtx JournalCtx(string mode) => Base(new MetaJournalCtx
        {
            Expedition = Live?.Raw,
            Lineage = Save?.Lineage,
            Mode = mode,
            Memo = Live != null ? Live.Memo : "",
            Scribbles = Live != null ? (IReadOnlyList<Scribble>)Live.Scribbles : Array.Empty<Scribble>(),
            OnMemo = text =>
            {
                if (Live == null) return;
                Live.Memo = text ?? "";
                Persist();
            },
            // journal.js imports EnemyPages itself; Trial.Script cannot, so the drawer is handed it.
            HasEnemyPage = EnemyPages.Has,
            EnemyPage = EnemyPages.Page,
        });

        // ------------------------------------------------------------------ main.js:121-210 the save

        // js: app/main.js:128
        /// <summary>
        /// persist(force): writes the save, unless another instance has written since this one last read or wrote it
        /// (then this save is stale and the stored one is reloaded: OnBecameStale). `force` writes anyway (はじめから).
        /// </summary>
        public bool Persist(bool force = false) => _keeper.Persist(force);

        // js: app/main.js:149
        /// <summary>persistSoon(): a recorded call is saved once the code that made it has run (one write per drain).</summary>
        private void PersistSoon() => _keeper.PersistSoon();

        // js: app/main.js:157
        /// <summary>inSync(): whether the stored save is still the one this instance last read or wrote.</summary>
        private bool InSync() => _keeper.InSync();

        // js: app/main.js:165
        /// <summary>
        /// markStale()'s screen half: another instance wrote the save (the keeper warned and stopped writing); this one
        /// reloads it a turn later (setTimeout 0).
        /// </summary>
        private void OnBecameStale() => PostTask(() => ReloadStored(true));

        // js: app/main.js:136
        /// <summary>
        /// persist's failure notice: a write failed after storage had worked (the keeper warned): say so once, over
        /// whatever is on screen (under the top bars).
        /// </summary>
        private void OnSaveFailed() => StageNotice(AppText.SaveFailedText, AppText.SaveFailedMs);

        // js: app/main.js:173
        /// <summary>reloadStored(notice): reads the stored save into this instance and shows the title (with STALE_TEXT).</summary>
        private void ReloadStored(bool notice)
        {
            if (_destroyed) return;
            // answer the open questions first: they were asked about the save this instance held
            foreach (ConfirmCardView c in Stage.OpenConfirms.ToList()) c.Cancel();
            _keeper.Reopen();
            Live = null;
            _failing = false;
            _audio?.DuckAmbience(1);
            ToTitle();
            if (notice) StageNotice(AppText.StaleText, AppText.StaleMs);
        }

        // js: app/main.js:189
        /// <summary>syncIdle(): on the title (nothing of the save is on screen), take up a save another instance wrote.</summary>
        private void SyncIdle() => _keeper.SyncIdle();

        // js: app/main.js:197 — window 'storage': not ported (no such event for files; see the file header).

        // ------------------------------------------------------------------ main.js:212-399 screens

        // js: app/main.js:213
        public void ToTitle()
        {
            Live = null;
            _screen = "title";
            _audio?.SetAmbience("title", 1200);
            Show(host => TitleScene.Mount(host, Base(new TitleCtx
            {
                // A saved lineage is something to continue even before its first life: the hall named the next heir
                // (INHERITANCE.md R-02) and つづきから brings the same heir back.
                HasSave = Save != null && Save.Lineage != null,
                // A stored save that did not load: つづきから says so, and はじめから asks over the title as it stands.
                Unreadable = Unreadable != null,
                OnNew = OnNew,
                OnContinue = () =>
                {
                    SyncIdle();
                    if (Save != null && Save.Life != null) ResumeLife();
                    else if (Save != null && Save.Lineage != null) ToHall();
                    else ToTitle();
                },
                OnSettings = () => OpenOverlay(ExploreOverlayKind.Settings),
            })), "ocbg-title");
            IReadOnlyList<string> lines = AppText.TitleNoticeLines(Unreadable != null, Unreadable != null && Unreadable.Kept, StorageOk);
            if (lines.Count > 0)
            {
                // Under the menu, inside the title's root and below its fade (z-index under the fade's), so it comes
                // and goes with the title; under any overlay too, and hidden while one is open (SceneNotices).
                RectTransform root = FindByClass(_sceneHost, "ocbg-meta-title");
                RectTransform parent = root ? root : _sceneHost;
                NoticeView note = Stage.NoticeLine(parent, string.Join("\n", lines), AppText.TitleNoticeX, AppText.TitleNoticeY(lines.Count), AppText.TitleNoticeWidth);
                _appNotices.Add(note);
                RectTransform fade = root ? FindByClass(root, "ocbg-meta-fade") : null;
                if (fade && fade.parent == root) note.Node.DomOrder = UiNode.Of(fade).DomOrder - 1e-3;   // insertBefore(note, fade)
                note.Node.Z = 40;
                UiNode.ResortSiblings(parent);
            }
        }

        // js: app/main.js:223 (toTitle's onNew)
        private void OnNew()
        {
            SyncIdle();
            void Fresh()
            {
                Unreadable = null;
                Save = new TrialSave(Inheritance.NewLineage(NewSeed()));
                Persist(true);
                ToHall();
            }
            if (Save != null && Save.Lineage != null && (Save.Life != null || Save.Lineage.Lives.Count > 0))
            {
                Stage.ConfirmCard(_confirmLayer, ConfirmOf(AppText.NewLineage)).Then(ok => { if (ok) Fresh(); });
            }
            else if (Unreadable != null)
            {
                // The question comes over the title as it stands (title.js asks first with ctx.unreadable), and
                // 「やめる」 leaves it there with the stored text untouched.
                Stage.ConfirmCard(_confirmLayer, ConfirmOf(AppText.NewOverUnreadable(Unreadable.Kept, Unreadable.Error))).Then(ok => { if (ok) Fresh(); });
            }
            else Fresh();
        }

        // js: app/main.js:259
        public void ToHall()
        {
            if (Stale) return;
            Live = null;
            _screen = "hall";
            if (Save == null || Save.Lineage == null)
            {
                Unreadable = null;
                Save = new TrialSave(Inheritance.NewLineage(_lineageSeed != 0 ? _lineageSeed : SaveKeeper.HallLineageSeed(NowMs())));
                Persist();
            }
            _audio?.SetAmbience("hall", 1500);
            Show(host => HallScene.Mount(host, Base(new HallCtx
            {
                Lineage = Save.Lineage,
                OnDepart = lifeStart => ToDeparture(lifeStart != null && lifeStart.Lineage != null ? lifeStart : Inheritance.StartLife(Save.Lineage)),
                // 設定・セーブ here offers 「保存してタイトルへ」: the lineage is already saved, so it reads as going back to the title.
                OnSettings = () => OpenOverlay(ExploreOverlayKind.Settings, OverlayExtra.LineageSave),
                OnTitle = ToTitle,
            })), "ocbg-hall");
        }

        // js: app/main.js:274
        private void ToDeparture(LifeStart lifeStart)
        {
            if (Stale) return;
            _screen = "departure";
            Show(host => DepartureScene.Mount(host, Base(new DepartureCtx
            {
                LifeStart = lifeStart,
                // The forty every character owns plus the prototype deck's kinds (Expedition owns its start deck's kinds too).
                Owned = DeckBuilder.WithPrototypeKinds(Expedition.InitialKinds),
                SavedDeck = string.IsNullOrEmpty(Save.DeckSave) ? null : Save.DeckSave,
                OnBack = ToHall,
                // 設定・セーブ opens here too: the gear at the top right, and Esc (CAMP_FACILITIES_DESIGN.md §3.5). The lineage is
                // saved and the half-built deck is kept as 出立's draft, so 「保存してタイトルへ」 leaves nothing behind.
                OnSettings = () => OpenOverlay(ExploreOverlayKind.Settings, OverlayExtra.LineageSave),
                OnSetOut = choice =>
                {
                    if (!InSync()) return;              // another instance moved the lineage on: this hall was out of date
                    object before = Inheritance.ToJson(Save.Lineage);
                    Save.Lineage = lifeStart.Lineage;             // the life is now in progress
                    Save.Life = LifeSave.Departing(before);
                    if (choice != null && !string.IsNullOrEmpty(choice.DeckSave)) Save.DeckSave = choice.DeckSave;
                    LifeStartData start = Trial.Bridge.Life.MakeStart(lifeStart, choice);
                    // the heir's own memo sheet starts empty; a survivor's journal comes in through start.journal
                    // (its pages, 開示度 and 走り書き, the survivor's memo among them: Inheritance.HeirJournal)
                    BeginLife(start, new LifeLog(), "", null);
                },
            })), "ocbg-departure");
        }

        // js: app/main.js:305
        /// <summary>
        /// beginLife(start, log, memo, scribbles): builds the life from its start and replays its log. A fight the log left
        /// in progress comes back where it stood and the battle scene takes it up (tier1 R1-16, #462); only a fight whose
        /// calls no longer replay (a save from another build) starts over, with a warning (Life.BeginLife).
        /// </summary>
        private void BeginLife(LifeStartData start, IReadOnlyList<LogEntry> log, string memo, object scribbles)
        {
            if (Stale) return;
            LiveLife live = Trial.Bridge.Life.BeginLife(start, log ?? new LifeLog(), memo ?? "", scribbles, Save?.Lineage, (tag, method) => PersistSoon());
            Live = live;
            Persist();
            if (live.OpenSource != null) ResumeFight(live.OpenSource);
            else ToExplore();
        }

        // js: app/main.js:329
        /// <summary>resumeFight(source): back into the fight a reload found in progress; the battle scene plays on from the source's frame.</summary>
        private void ResumeFight(RecordedSource source)
        {
            _screen = "battle";
            _audio?.SetAmbience(Live.Raw.Session.State.Profile.Layer, 1200);
            RunEncounter(source).Then(() =>
            {
                if (Stale || Live == null) return;
                Persist();
                ToExploreBack();
            }).Forget();
        }

        // js: app/main.js:337
        /// <summary>
        /// resumeLife(): a life that does not replay (a save from another build, a bug): nothing is overwritten until the
        /// player decides. The stored save is copied aside first (keepBrokenSave); 「あきらめる」 drops the life and the
        /// lineage goes back to how it stood before that departure; 「タイトルへ」 leaves the save as it is.
        /// </summary>
        private void ResumeLife()
        {
            LifeSave l = Save?.Life;
            try
            {
                if (l == null) throw new SaveFormatException("bad save: no life to resume");
                BeginLife(l.ReadStart(), l.ReadLog(), l.Memo, l.Scribbles);
            }
            catch (Exception e)
            {
                Debug.LogError("resume failed; the life is kept until the player decides: " + e);
                Live = null;
                bool kept = Saves.KeepBrokenSave(_store);
                ToTitle();
                Stage.ConfirmCard(_confirmLayer, ConfirmOf(AppText.CannotResume(kept, e))).Then(ok =>
                {
                    if (!ok || Save == null || !ReferenceEquals(Save.Life, l)) return;
                    if (l != null && l.Before != null)
                    {
                        try { Save.Lineage = Inheritance.FromJson(l.Before); }
                        catch (Exception) { /* keep */ }
                    }
                    Save.Life = null;
                    Persist();
                    ToHall();
                });
            }
        }

        // js: app/main.js:360
        private void ToExplore()
        {
            if (Stale || Live == null) return;
            _screen = "explore";
            Expedition raw = Live.Raw;
            RecordedExpedition expedition = Live.Expedition;
            Show(host => ExploreScene.Mount(host, new ExploreSceneCtx
            {
                Stage = _stage,
                Audio = _audio,
                Settings = _settings,
                Expedition = expedition,
                Inheritance = new ExploreInheritance
                {
                    TraceView = (layer, nodeId) => Inheritance.TraceNodeView(Save.Lineage, layer),
                    // The 走り書き the life carries now: the trace card says which of the trace's it already holds.
                    Scribbles = () => Live != null ? (IReadOnlyList<Scribble>)Live.Scribbles.ToList() : Array.Empty<Scribble>(),
                    Take = picks => Take(raw, picks),
                },
                OnBattle = () => RunEncounter().Then(() =>
                {
                    if (Stale || Live == null) return;
                    Persist();
                    ToExploreBack();
                }),
                OnLifeEnd = ending => EndLife(),
                OnOverlay = (kind, info) =>
                {
                    OpenOverlay(kind);
                    return null;
                },
                OnSaveQuit = SaveQuit,
                OnPersist = () => Persist(),
                OnCarving = () =>
                {
                    if (!InSync()) return;
                    Save.Lineage = Inheritance.NoteCarving(Save.Lineage);
                    Persist();
                },
            }), "ocbg-explore");
        }

        // js: app/main.js:370 (toExplore's inheritance.take)
        /// <summary>
        /// inheritance.take(picks): the trace on this layer is taken (Inheritance.TakeTrace) and what it gives goes into the
        /// life (Expedition.Inherit, logged); the lineage and the E.Inherit entry land in one write (INHERITANCE R-21).
        /// Null when nothing was taken.
        /// </summary>
        private ExploreTaken Take(Expedition raw, IReadOnlyList<string> picks)
        {
            if (!InSync()) return null;       // another instance collected or moved on: nothing is taken here
            // A life that is over takes nothing (the 瘴気死 on the trace's own step): the trace stays for a living heir
            // (INHERITANCE R-09 / R-10, EXTENSIONS X-3). The scene never offers it then; this is the second guard.
            if (Live == null || raw.Session.State.IsOver) return null;
            int layer = raw.Session.State.Profile.Layer;
            TraceNodeView view = Inheritance.TraceNodeView(Save.Lineage, layer);
            if (view == null || !view.CanTake) return null;
            TakeResult r = Inheritance.TakeTrace(Save.Lineage, view.TraceId, picks ?? Array.Empty<string>(), layer);
            Save.Lineage = r.Lineage;
            // What the take did (the 段 and 開示度 this heir ends with, the copies that joined the deck): the
            // 受け取った遺産 card is written from it (TraceGain), not from the dead heir's numbers.
            InheritResult result = Live.Expedition.Inherit(r.Taken) ?? new InheritResult(null, null, 0);
            AddedScribbles added = Trial.Bridge.Life.AddScribbles(Live.Scribbles, r.Taken.Journal != null ? r.Taken.Journal.Scribbles : null);
            Live.Scribbles = added.List;
            Persist();
            return new ExploreTaken(r.Taken, result.WithScribbles(added.Rows));
        }

        // js: app/main.js:399
        /// <summary>After the fights the explore scene comes back with the result card (it calls Return itself).</summary>
        private void ToExploreBack() => ToExplore();

        // ------------------------------------------------------------------ main.js:401-466 fights

        // js: app/main.js:409
        /// <summary>
        /// autoFight(source): every turn plays the source's AutoCardId (not SuggestedCardId: a fight a reload brought back
        /// was replayed as the player's page built it, StartNext(false), with no suggestions), else ends the turn. The JS's
        /// `try { Next() } catch { AdvanceAuto() }` is AdvanceAuto (it takes the pending event, or begins the turn).
        /// </summary>
        private static void AutoFight(ITrialSource source)
        {
            for (int guard = 0; guard < 20000 && !source.Finished; guard++)
            {
                if (source.WaitingForPlayer)
                {
                    string id = source.AutoCardId;
                    CardFace face = !string.IsNullOrEmpty(id) ? DepictionText.Find(source.Frame.Hand, id) : null;
                    if (face != null) source.TryPlay(id, DepictionText.RequiredZone(face.Aim), out DepictionEvent _);
                    else source.EndTurn();
                }
                else source.AdvanceAuto();
            }
        }

        // js: app/main.js:429
        /// <summary>
        /// runEncounter(resumed): fights every fight of the waiting encounter, one battle scene per fight. `resumed` is the
        /// recorded source of a fight a reload found in progress (BeginLife): it is fought first, from where it stood.
        /// </summary>
        private async Promise RunEncounter(RecordedSource resumed = null)
        {
            RecordedExpedition ex = Live.Expedition;
            RecordedBattle battle = ex.Battle ?? ex.StartEncounter();
            if (battle == null) return;
            _audio?.DuckAmbience(BattleDuck);
            while (!battle.Over)
            {
                // The fight is finished and saved the moment the core decides it, before the fall and the result
                // card play (recorder.settleWhenDecided, tier1 R1-16): a reload there keeps the outcome. Every call
                // before that is saved as it is made (persistSoon), so a reload mid-fight comes back to the fight.
                SettlingFight fight = Recorder.SettleWhenDecided(battle, resumed ?? battle.StartNext(_autobattle), () => Persist());
                resumed = null;
                _fightNow = fight.Source;
                if (fight.Settled) continue;
                if (_autobattle)
                {
                    AutoFight(fight.Source);
                    fight.Finish();
                    continue;
                }
                _screen = "battle";
                var finished = new PromiseSource();
                Show(host => BattleScene.MountBattleScene(host, BattleCtx(battle, fight, finished.Resolve)), "ocbg-battle");
                await finished.Promise;
                fight.Finish();   // a no-op once the decision finished it
            }
            _audio?.DuckAmbience(1);
        }

        // js: app/main.js:445 (runEncounter's show(mountBattleScene, ctx))
        private BattleSceneCtx BattleCtx(RecordedBattle battle, SettlingFight fight, Action onFinished)
        {
            Expedition raw = Live.Raw;
            return new BattleSceneCtx
            {
                Stage = _stage,
                Audio = _audio,
                Settings = _settings,
                Source = fight.Source,
                Layer = raw.Session.State.Profile.Layer,
                LayerTitle = raw.Session.Screen().LayerTitle,
                MiasmaPercent = battle.HandIn.MiasmaPercent,
                NodeKind = BattleSceneCtx.NodeKindOf(battle.HandIn.Kind.ToString()),
                Chain = new BattleChain(battle.Index + 1, battle.Count),
                DisclosureOf = id => raw.DisclosureOf(id),
                OpenJournal = (host, opts) =>
                {
                    MetaJournalCtx ctx = JournalCtx("battle");
                    if (opts != null)
                    {
                        ctx.EnemyIds = opts.EnemyIds;
                        ctx.DisclosureOf = opts.DisclosureOf;
                        ctx.OnClose = opts.OnClose;
                    }
                    return JournalDrawer.Mount(host, ctx);
                },
                // Esc and the gear open 設定・セーブ in a fight too, with 「保存して閉じる」: a fight is saved move by move
                // (tier1 R1-16, #462), so つづきから comes back to the same moment (settings.js 'battle').
                SettingsInBattle = true,
                OnSettings = () => OpenOverlay(ExploreOverlayKind.Settings, OverlayExtra.Battle),
                OnSettingsChange = next =>
                {
                    if (next != null) _settings.Assign(next);
                    Saves.WriteSettings(_store, _settings);
                    _settings.NotifyChanged();
                },
                OnError = err => BattleFailed(err),
                OnFinished = onFinished,
            };
        }

        // js: app/main.js:472
        /// <summary>
        /// battleFailed(err): the battle scene could not go on (the source threw; the call that threw is not in the log).
        /// The fight in progress is dropped from the save (trimUnfinished) and the life is loaded again: the node waits on
        /// the same fight, which starts over.
        /// </summary>
        private void BattleFailed(Exception err)
        {
            if (_failing) return;
            _failing = true;
            Debug.LogError("battle failed: " + err);
            if (Live != null) Live.Log.ReplaceAll(LifeLog.TrimUnfinished(Live.Log));
            Persist();
            Stage.ConfirmCard(_confirmLayer, ConfirmOf(AppText.BattleFailed(err))).Then(ok =>
            {
                _failing = false;
                if (ok) ResumeLife();
                else ToTitle();
            });
        }

        // js: app/main.js:485
        /// <summary>endLife(): the life's end goes on the lineage (recordEnd) and is saved before the ending mounts.</summary>
        private void EndLife()
        {
            if (Live == null || !InSync()) return;         // another instance holds a newer save: this death or survival is not recorded here
            Expedition raw = Live.Raw;
            ExplorationState state = raw.Session.State;
            if (!state.IsOver || state.Phase == RunPhase.Exploring) return;
            LifePlace place = Inheritance.PlaceFromState(state);
            var journal = Names.LifeJournal(raw, Live);
            EndOptions opts = Names.EndOptions(raw, state);
            Save.Lineage = Inheritance.RecordEnd(Save.Lineage, null, place, raw.Mastery, journal, opts);
            Save.Life = null;
            Live = null;
            Persist();
            _audio?.SetAmbience("hall", 2000);
            _screen = "ending";
            Show(host => EndingScene.Mount(host, Base(new EndingCtx
            {
                Ending = Inheritance.EndingView(Save.Lineage),
                Lineage = Save.Lineage,
                OnHall = ToHall,
            })), "ocbg-ending");
        }

        // js: app/main.js:504
        /// <summary>
        /// saveQuit(): 「保存して閉じる」 / 「保存してタイトルへ」: everything is written, then the title. From a fight the
        /// battle scene is dropped where it stands (its moves are in the log: つづきから replays them).
        /// </summary>
        private void SaveQuit()
        {
            Persist();
            CloseOverlay();
            _audio?.DuckAmbience(1);
            ToTitle();
        }

        // ------------------------------------------------------------------ main.js:511 boot

        // js: app/main.js:512
        /// <summary>start(): the title (the hot-update snapshot is TrialBoot's flush on pause / quit).</summary>
        public void Start() => ToTitle();

        // ------------------------------------------------------------------ the frame, teardown, helpers

        /// <summary>setTimeout(fn, 0): runs at the next RunTasks (the next frame).</summary>
        public void PostTask(Action task)
        {
            if (task != null && !_destroyed) _tasks.Add(task);
        }

        /// <summary>
        /// Runs the tasks posted until now (TrialBoot.Update). Each runs as the browser runs a setTimeout callback: then the
        /// microtask checkpoint. It is run from inside the drain, so what it settles waits until it has returned, as a
        /// promise's .then does in the JS: reloadStored cancels the open confirm cards first, and their .then(false) runs
        /// after the stored save is read again and the title is up (Kit's button Press drains, which is a no-op inside a drain).
        /// </summary>
        public void RunTasks()
        {
            if (_tasks.Count == 0) return;
            Action[] now = _tasks.ToArray();
            _tasks.Clear();
            foreach (Action t in now)
            {
                if (_destroyed) return;
                Microtasks.Post(() =>
                {
                    if (!_destroyed) t();   // an exception goes to Microtasks.Unhandled (TrialBoot logs it)
                });
                Microtasks.Drain();
            }
        }

        /// <summary>The shell goes (play mode ends, TrialBoot destroyed): the overlay, the scene and the router's layers.</summary>
        public void Destroy()
        {
            if (_destroyed) return;
            _destroyed = true;
            _tasks.Clear();
            _keeper.BecameStale -= OnBecameStale;
            _keeper.SaveFailed -= OnSaveFailed;
            CloseOverlay(refresh: false);
            IScene scene = _scene;
            _scene = null;
            if (scene != null)
            {
                try { scene.Destroy(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            foreach (RectTransform l in _sceneLayers) RemoveLayer(l);
            _sceneLayers.Clear();
            _appNotices.Clear();
            _stageNotices.Clear();
            RemoveLayer(_noticeLayer);
            RemoveLayer(_confirmLayer);
        }

        /// <summary>
        /// layer.remove(): hidden at once and destroyed at the end of the frame, but left where it stands until then, so a
        /// layer made in the same frame still finds its place among the stage's ordered layers (TrialStage.Layer reads the
        /// sibling index of the layer above it; a detached one would have none).
        /// </summary>
        private static void RemoveLayer(RectTransform layer)
        {
            if (!layer) return;
            GameObject go = layer.gameObject;
            go.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>A notice on the stage root (z 880, under the top bars): SAVE_FAILED_TEXT, STALE_TEXT.</summary>
        private void StageNotice(string text, float ms)
        {
            if (!_noticeLayer) return;
            _stageNotices.RemoveAll(n => n == null || !n.Root);
            _stageNotices.Add(Stage.NoticeLine(_noticeLayer, text, AppText.StageNoticeX, AppText.StageNoticeY, AppText.StageNoticeWidth, ms));
        }

        /// <summary>Object.assign({ stage, audio, settings }, ctx).</summary>
        private T Base<T>(T ctx) where T : MetaCtx
        {
            ctx.Stage = _stage;
            ctx.Audio = _audio;
            ctx.Settings = _settings;
            return ctx;
        }

        private static ConfirmOpts ConfirmOf(ConfirmText t) => new ConfirmOpts
        {
            Title = t.Title,
            Body = t.Body,
            Ok = t.Ok,
            Cancel = t.Cancel,
            Danger = t.Danger,
        };

        // js: app/main.js:225 — (Date.now() ^ (Math.random() * 0x7fffffff)) >>> 0, or TrialBoot's fixed seed.
        private ulong NewSeed() => _lineageSeed != 0 ? _lineageSeed : SaveKeeper.NewLineageSeed(NowMs(), _random.NextDouble());

        private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>root.querySelector('.' + cls): the first element below root (document order) whose name holds the class.</summary>
        private static RectTransform FindByClass(Transform root, string cls)
        {
            if (!root) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (HasClass(c.name, cls) && c is RectTransform hit) return hit;
                RectTransform deeper = FindByClass(c, cls);
                if (deeper) return deeper;
            }
            return null;
        }

        private static bool HasClass(string name, string cls)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (string part in name.Split(' '))
                if (part == cls) return true;
            return false;
        }

        /// <summary>el.offsetTop within `ancestor`: the layout tops (CSS transforms left out) summed up to it.</summary>
        private static float OffsetTopIn(RectTransform el, Transform ancestor)
        {
            float y = 0f;
            for (Transform t = el; t != null && t != ancestor; t = t.parent)
                if (t is RectTransform rt) y += Ui.PosOf(rt).y;
            return y;
        }
    }
}
