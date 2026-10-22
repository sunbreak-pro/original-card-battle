// The trial's scene component (port of the boot half of src/app/main.js:19-35, :404-407, :512-524): put it on one
// GameObject of an empty scene and press Play. It builds what main.js builds before the first screen — the stage, the
// settings, the audio, the save — makes the router (Main) and starts it on the title. Every frame it runs the router's
// setTimeout tasks (Update) and writes a save that persistSoon queued (LateUpdate); a queued write is also flushed when
// the application pauses, loses focus, quits, or this component goes (the browser's hot-update snapshot, app.md §1.1).
//
// The Inspector switches stand for the JS's URL switches and browser facts:
//   autobattle    ?autobattle: every fight fought unattended with the source's AutoCardId (no battle scene)
//   debug         ?debug: TrialBoot.DebugView (window.__ocb) and the context-menu commands (dump / open / delete / stale)
//   saveFolder    the folder under Application.persistentDataPath the save files live in ("trial")
//   noStorage     storage blocked (a private window): the game plays from memory and says nothing is kept
//   lineageSeed   はじめから's lineage seed (0: the JS's time ^ random)
//   reducedMotion prefers-reduced-motion: reduce (CssTime.ReducedMotion)
//   vSync         requestAnimationFrame pacing (vSyncCount 1); off: 60 fps
//
// The browser waits for document.fonts.ready (≤ 1500 ms) before the title; here the font assets are made in Awake.
// Domain reload is off in the project: the statics are reset at SubsystemRegistration.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Trial.Audio;
using Trial.Bridge;
using Trial.Kit;
using Trial.Meta;
using UnityEngine;
using Saves = Trial.Bridge.Save;

namespace Trial.Boot
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Trial/Trial Boot")]
    public sealed class TrialBoot : MonoBehaviour
    {
        // ------------------------------------------------------------------ Inspector (main.js:404-407 and the browser)

        [Tooltip("?autobattle: every fight is fought unattended with the source's AutoCardId (no battle scene is mounted).")]
        public bool autobattle;

        [Tooltip("?debug: TrialBoot.DebugView (window.__ocb) and the context-menu commands.")]
        public bool debug;

        [Tooltip("The folder under Application.persistentDataPath the save files live in.")]
        public string saveFolder = "trial";

        [Tooltip("Storage blocked (a private window): the game plays from memory and says nothing is kept.")]
        public bool noStorage;

        [Tooltip("はじめから's lineage seed. 0: the JS's (Date.now() ^ random) >>> 0.")]
        public long lineageSeed;

        [Tooltip("prefers-reduced-motion: reduce (CSS animations and meta tweens 1 ms, the exploration's short timings).")]
        public bool reducedMotion;

        [Tooltip("vSyncCount 1 (requestAnimationFrame pacing); off: targetFrameRate 60.")]
        public bool vSync = true;

        // ------------------------------------------------------------------ what main.js:19-35 builds

        private TrialStage _stage;
        private ITrialAudio _audio;
        private TrialAudio _ownAudio;
        private TrialSettings _settings;
        private IKeyValueStore _store;
        private SaveKeeper _keeper;
        private Main _main;
        private bool _started;

        /// <summary>The live boot (the last one awake), or null.</summary>
        public static TrialBoot Current { get; private set; }

        /// <summary>window.__ocb: the save, the live life, the fight, the settings, the audio, the stage. Null unless debug is on.</summary>
        public static TrialDebug DebugView { get; private set; }

        /// <summary>The stage (createStage).</summary>
        public TrialStage StageObject => _stage;

        /// <summary>The game's sound (createAudio), or null when it could not be made.</summary>
        public ITrialAudio Audio => _audio;

        /// <summary>The one shared settings object (DEFAULT_SETTINGS + loadSettings()).</summary>
        public TrialSettings Settings => _settings;

        /// <summary>The storage under the save (localStorage).</summary>
        public IKeyValueStore Store => _store;

        /// <summary>The save this instance holds and writes (openSave, persist).</summary>
        public SaveKeeper Keeper => _keeper;

        /// <summary>The router (the rest of main.js).</summary>
        public Main Router => _main;

        // ------------------------------------------------------------------ boot

        // js: app/main.js:19
        private void Awake()
        {
            if (Current && Current != this)
            {
                Debug.LogWarning("[trial] a second TrialBoot: this one stays idle");
                enabled = false;
                return;
            }
            Current = this;
            Microtasks.Unhandled = e => Debug.LogException(e);
            Saves.Warn = (message, e) => Debug.LogWarning("[trial] " + message + (e == null ? "" : ": " + e));

            QualitySettings.vSyncCount = vSync ? 1 : 0;
            if (!vSync) Application.targetFrameRate = 60;
            CssTime.ReducedMotion = reducedMotion;
            // js: app/main.js:523 — document.fonts.ready: the eight faces are made before the title.
            TrialFonts.LoadAll();

            // js: app/main.js:21 — createStage(app)
            _stage = TrialStage.Create("TrialStage");

            // localStorage (save.js:19-27): a file per key, or blocked storage
            _store = noStorage
                ? new MemoryKeyValueStore { Blocked = true }
                : (IKeyValueStore)new FileKeyValueStore(Path.Combine(Application.persistentDataPath, string.IsNullOrEmpty(saveFolder) ? "trial" : saveFolder));

            // 出立's draft (departure.js:62 ocb.departure.draft.v1) lives in the same localStorage: departure-draft.v1.json
            // beside the save (app.md §2.1). Without this the departure scene falls back to PlayerPrefs.
            DepartureScene.DraftStore = _store;

            // js: app/main.js:22 — Object.assign({}, DEFAULT_SETTINGS, loadSettings())
            _settings = TrialSettings.Load(_store);

            // js: app/main.js:23 — createAudio(OCB, settings, { onChange: s => { Object.assign(settings, s); writeSettings(settings) } })
            _ownAudio = MakeAudio(_settings, OnAudioChanged);
            _audio = _ownAudio;

            // js: app/main.js:26-45 — openSave(), TAB, storageWorks()
            _keeper = SaveKeeper.Open(_store);

            _main = new Main(new MainOptions
            {
                Stage = _stage,
                Audio = _audio,
                Settings = _settings,
                Store = _store,
                Keeper = _keeper,
                Autobattle = autobattle,
                LineageSeed = unchecked((ulong)lineageSeed),
            });

            // js: app/main.js:407 — ?debug exposes window.__ocb
            if (debug) DebugView = new TrialDebug(this);
        }

        // js: app/main.js:519 (boot → start)
        private void Start()
        {
            if (_main == null || _started) return;
            _started = true;
            _main.Start();
            Microtasks.Drain();
        }

        /// <summary>
        /// The audio's own change (a slider moved the volume, the mute key): the settings take the audio's values and are
        /// written (main.js:23 Object.assign(settings, s); writeSettings(settings)).
        /// </summary>
        private void OnAudioChanged()
        {
            if (_audio == null || _settings == null || _store == null) return;
            _settings.Master = _audio.Master;
            _settings.Effects = _audio.Effects;
            _settings.Ambience = _audio.AmbienceVolume;
            _settings.Reverb = _audio.Reverb;
            _settings.Muted = _audio.Muted;
            Saves.WriteSettings(_store, _settings);
            _settings.NotifyChanged();
        }

        /// <summary>
        /// TrialAudio.Create(master, effects, ambience, muted, onChange, unlock: true), through reflection: both Create
        /// overloads and the onChange listener name Trial.Audio.Synth's SoundSettings, which Trial.Boot does not reference,
        /// so the compiler cannot bind them here. The listener ignores the snapshot and reads the audio's own values.
        /// Null (silent) when the audio cannot be made.
        /// </summary>
        private static TrialAudio MakeAudio(TrialSettings s, Action onChange)
        {
            try
            {
                MethodInfo create = typeof(TrialAudio).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Create" && m.GetParameters().Length == 6 && m.GetParameters()[0].ParameterType == typeof(double));
                if (create == null) return null;
                Delegate listener = null;
                Type listenerType = create.GetParameters()[4].ParameterType;   // Action<SoundSettings>
                if (onChange != null && listenerType.IsGenericType)
                {
                    MethodInfo make = typeof(TrialBoot).GetMethod(nameof(Listener), BindingFlags.NonPublic | BindingFlags.Static);
                    listener = (Delegate)make.MakeGenericMethod(listenerType.GetGenericArguments()[0]).Invoke(null, new object[] { onChange });
                }
                return (TrialAudio)create.Invoke(null, new object[] { s.Master, s.Effects, s.Ambience, s.Muted, listener, true });
            }
            catch (Exception e)
            {
                Debug.LogException(e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e);
                return null;
            }
        }

        private static Action<T> Listener<T>(Action fn) => _ => fn();

        // ------------------------------------------------------------------ the frame

        private void Update()
        {
            // setTimeout(fn, 0) of the router (markStale's reloadStored)
            _main?.RunTasks();
        }

        private void LateUpdate()
        {
            // persistSoon's write, at the latest at the end of the frame that queued it
            Flush();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnApplicationQuit()
        {
            Flush();
        }

        private void OnDestroy()
        {
            if (Current != this) return;
            Flush();
            try { _main?.Destroy(); }
            catch (Exception e) { Debug.LogException(e); }
            _main = null;
            if (_ownAudio != null)
            {
                try { _ownAudio.Destroy(); }
                catch (Exception e) { Debug.LogException(e); }
                _ownAudio = null;
            }
            _audio = null;
            if (_stage) Destroy(_stage.gameObject);
            if (ReferenceEquals(DepartureScene.DraftStore, _store)) DepartureScene.DraftStore = null;
            Current = null;
            DebugView = null;
        }

        /// <summary>Writes a save persistSoon queued and has not written yet.</summary>
        private void Flush()
        {
            if (_keeper == null) return;
            try { _keeper.FlushIfDirty(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ------------------------------------------------------------------ debug (?debug)

        private string SaveFolderPath => _store is FileKeyValueStore f ? f.Folder : null;

        [ContextMenu("Trial/Dump save JSON")]
        private void DumpSave()
        {
            if (!debug || _store == null) return;
            string text = _store.Read(Saves.SaveKey);
            Debug.Log("[trial] " + Saves.SaveKey + (text == null ? ": nothing stored" : " (" + text.Length + " chars)\n" + text));
        }

        [ContextMenu("Trial/Open save folder")]
        private void OpenSaveFolder()
        {
            string folder = SaveFolderPath;
            if (!debug || folder == null) return;
            Directory.CreateDirectory(folder);
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }

        [ContextMenu("Trial/Delete save")]
        private void DeleteSave()
        {
            if (!debug || _store == null) return;
            Saves.ClearAll(_store);
            _store.Remove(Saves.BrokenKey);
            // 出立's draft is keyed by the lineage seed: a fixed lineageSeed would read the deleted lineage's draft back
            _store.Remove(DepartureScene.DraftKey);
            Debug.Log("[trial] the save was deleted (the game in progress keeps its own until it writes)");
        }

        [ContextMenu("Trial/Force stale (another instance wrote)")]
        private void ForceStale()
        {
            if (!debug || _keeper == null) return;
            _keeper.MarkStale();
        }

        // ------------------------------------------------------------------ domain reload off

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            DebugView = null;
            // a microtask the last play session left must not run in this one
            Microtasks.Reset();
        }
    }

    /// <summary>window.__ocb (main.js:407): what a debugging session reads of the running game.</summary>
    public sealed class TrialDebug
    {
        private readonly TrialBoot _boot;

        internal TrialDebug(TrialBoot boot)
        {
            _boot = boot;
        }

        /// <summary>save: { lineage, life, deckSave }.</summary>
        public TrialSave Save => _boot ? _boot.Router?.Save : null;

        /// <summary>live: the life in progress.</summary>
        public LiveLife Live => _boot ? _boot.Router?.Live : null;

        /// <summary>fight: the source of the fight on screen.</summary>
        public Depiction.Bridge.ITrialSource Fight => _boot ? _boot.Router?.FightNow : null;

        public TrialSettings Settings => _boot ? _boot.Settings : null;
        public ITrialAudio Audio => _boot ? _boot.Audio : null;
        public TrialStage Stage => _boot ? _boot.StageObject : null;

        /// <summary>The screen up now (title / hall / departure / explore / battle / ending).</summary>
        public string Screen => _boot ? _boot.Router?.ScreenName : null;
    }
}
