// The battle scene: mounts the uGUI renderer of one fight's depiction script into the stage (port of
// scenes/battle/BattleScene.js; Unity's battle screen, Assets/View/Depiction/DepictionPlayer.cs and its views).
//
//   BattleScene.MountBattleScene(host, ctx) → BattleSceneHandle { Destroy, Refresh, Pause }
//   ctx = BattleSceneCtx { Stage, Audio, Settings, Source, Layer (1..7), NodeKind, Chain { Index, Total }, DisclosureOf,
//         OpenJournal(host, opts) → IOverlay, SettingsInBattle, OnSettings, OnFinished, OnSettingsChange, OnError, Auto }
//
// The scene computes no rule: BattlePlayer plays ctx.Source's frames / events / cues and sends input back to ctx.Source
// (docs/BATTLE_SCENE.md). This file builds the root, the layers, the shared env, the three view modules (hand / field /
// hud) and the player, and tears it all down; BattleOverlays.cs holds the journal and the settings over the battle,
// BattleInput.cs the keys, the pointer and the fast-forward, BattleSceneDriver.cs the frame.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Depiction;
using Trial.Audio;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>
    /// The scene's ctx as the app hands it (main.js runEncounter → mountBattleScene; app.md §5.5 BattleCtx). The engine-free
    /// <see cref="BattleCtx"/> the player reads is made from it.
    /// </summary>
    // js: scenes/battle/BattleScene.js:4 and src/app/main.js:445
    public sealed class BattleSceneCtx
    {
        /// <summary>ctx.source: the battle — the recorded source (Trial.Bridge.RecordedSource) or a CoreBattleSource. Required.</summary>
        public IDepictionSource Source;
        /// <summary>ctx.stage: the app's stage (toStage; null for a host on a canvas of its own).</summary>
        public TrialStage Stage;
        /// <summary>ctx.audio (null: silent).</summary>
        public ITrialAudio Audio;
        /// <summary>ctx.settings: speed and 演出短縮 read live; the speed switch writes Speed (null: the defaults, not kept).</summary>
        public TrialSettings Settings;
        /// <summary>ctx.layer: the exploration's layer, 1..7.</summary>
        public int Layer = 1;
        /// <summary>ctx.layerTitle.</summary>
        public string LayerTitle = "";
        /// <summary>ctx.miasmaPercent: the exploration's miasma % (null: the frame's, which the writer leaves 0).</summary>
        public float? MiasmaPercent;
        /// <summary>ctx.nodeKind ('Battle' | 'Elite' | 'Boss').</summary>
        public BattleNodeKind NodeKind = BattleNodeKind.Battle;
        /// <summary>ctx.chain: { index, total } — the 1-based fight under way and how many the chain holds (null: { 1, 1 }).</summary>
        public BattleChain Chain;
        /// <summary>ctx.disclosureOf(enemyId) → the journal's 開示度.</summary>
        public Func<string, int> DisclosureOf;
        /// <summary>ctx.openJournal(host, opts) → the journal mounted into host (the battle's over layer); its Destroy is the JS destroy().</summary>
        public Func<RectTransform, BattleJournalOpts, IOverlay> OpenJournal;
        /// <summary>ctx.settingsInBattle: the gear and Esc's last step open the settings in a fight (the game passes true).</summary>
        public bool SettingsInBattle;
        /// <summary>ctx.onSettings(): open the app's settings over the battle (it calls Refresh when it closes).</summary>
        public Action OnSettings;
        /// <summary>ctx.onSettingsChange(settings): the speed switch changed Settings.Speed in place (persist it).</summary>
        public Action<TrialSettings> OnSettingsChange;
        /// <summary>ctx.onError(err): the source threw; the battle stopped (halted).</summary>
        public Action<Exception> OnError;
        /// <summary>ctx.onFinished(): the result has been read; called exactly once.</summary>
        public Action OnFinished;
        /// <summary>ctx.auto (dev / tests): the player plays itself.</summary>
        public BattleAuto Auto;
        /// <summary>Where the scene's console.error goes (null: Debug.LogError / Debug.LogException).</summary>
        public IBattleLog Log;

        /// <summary>battle.HandIn.Kind ('Battle' | 'Elite' | 'Boss') → the node kind (anything else: Battle, the JS default).</summary>
        public static BattleNodeKind NodeKindOf(string kind) =>
            kind == "Elite" ? BattleNodeKind.Elite : kind == "Boss" ? BattleNodeKind.Boss : BattleNodeKind.Battle;
    }

    /// <summary>The scene's modules: { hand, field, hud } (BattleScene.js:515).</summary>
    public sealed class BattleSceneModules
    {
        public Hand Hand;
        public FieldView FieldView;
        public Hud Hud;
    }

    /// <summary>
    /// mountBattleScene's answer: { root, env, player, modules, refresh(), pause(on), destroy() }. Holds the JS closures of
    /// mountBattleScene as its members (battle-flow §4.3).
    /// </summary>
    // js: scenes/battle/BattleScene.js:511
    public sealed class BattleSceneHandle : IScene
    {
        internal readonly BattleSceneCtx Ctx;
        internal readonly BattleViewEnv Env;
        internal Hand HandView;
        internal FieldView FieldModule;
        internal Hud HudView;
        internal BattlePlayer PlayerCore;
        internal BattleOverlays Overlays;
        internal BattleInput Input;
        internal BattleSceneDriver Driver;
        internal bool Destroyed;
        internal bool RootDying;
        private bool _started;
        private readonly List<BattleLayer> _opening = new List<BattleLayer>();

        internal BattleSceneHandle(BattleSceneCtx ctx, BattleViewEnv env)
        {
            Ctx = ctx;
            Env = env;
        }

        /// <summary>root: the scene root.</summary>
        public RectTransform Root => Env.Root;

        /// <summary>env.</summary>
        public BattleViewEnv BattleEnv => Env;

        /// <summary>player.</summary>
        public BattlePlayer Player => PlayerCore;

        /// <summary>modules: { hand, field, hud }.</summary>
        public BattleSceneModules Modules => new BattleSceneModules { Hand = HandView, FieldView = FieldModule, Hud = HudView };

        /// <summary>The scene has been destroyed.</summary>
        public bool IsDestroyed => Destroyed;

        /// <summary>
        /// refresh(): the app closed an overlay it opened over the battle (main.js closeOverlay) — the scene runs on, and the
        /// speed switch shows the speed the settings may have changed (演出短縮 is read live by the clock).
        /// </summary>
        // js: scenes/battle/BattleScene.js:520
        public void Refresh()
        {
            if (Destroyed) return;
            Overlays.SettingsClosed();
            PlayerCore.ShowSpeed();
            Microtasks.Drain();
        }

        /// <summary>pause(on): for the app — hold the scene still (true) or let it run (false) while something of its own covers it.</summary>
        // js: scenes/battle/BattleScene.js:526
        public void Pause(bool on)
        {
            if (Destroyed) return;
            Overlays.SetSettingsOpen(on);
            Overlays.SyncPause();
            Microtasks.Drain();
        }

        /// <summary>destroy(): in the JS order — listeners off, the journal closed, the player and the modules destroyed, the clocks stopped, the root removed.</summary>
        // js: scenes/battle/BattleScene.js:530
        public void Destroy()
        {
            if (Destroyed) return;
            Destroyed = true;
            Input.DetachState();
            Input.DetachEventEnd();
            Input.DetachKeys();
            Input.DetachKeyUp();
            Input.DetachPointer();
            Input.DetachBlur();
            Overlays.CloseJournal();
            PlayerCore.Destroy();
            foreach (Action destroy in new Action[] { HandView.Destroy, HudView.Destroy, FieldModule.Destroy })
            {
                try { destroy(); }
                catch (Exception err) { Env.Log.Error(null, err); }
            }
            Env.Clock.Destroy();
            if (Driver) Driver.Scene = null;
            if (!RootDying && Env.Root) Ui.Remove(Env.Root);
        }

        // ---- mount ----------------------------------------------------------------------------------------

        // js: scenes/battle/BattleScene.js:130
        internal void Mount()
        {
            BattleViewEnv env = Env;
            BattleSceneCtx ctx = Ctx;

            // The modules raise input through callbacks; the player is made last, so they are bound late.
            // js: scenes/battle/BattleScene.js:144
            FieldModule = FieldView.Create(env, new FieldOpts
            {
                Layer = env.LayerNo,
                NodeKind = env.NodeKind,
                // 俊敏's choice on the floor (battle_ui_ux_v2 §3.8): a click on a mark, the pointer over one.
                OnFreeStepPick = dir => { if (PlayerCore != null) PlayerCore.PickFreeStep(dir); },
                OnFreeStepFocus = dir => { if (PlayerCore != null) PlayerCore.FocusFreeStep(dir); },
            });
            // js: scenes/battle/BattleScene.js:151
            BattleChain chain = ctx.Chain ?? new BattleChain(1, 1);
            HudView = Hud.Create(env, new HudOpts
            {
                ChainIndex = chain.Index,
                ChainTotal = chain.Total,
                NodeKind = env.NodeKind,
                // The plate, like X, is not lost to a refused card's slide home (it waits for it); a press while the flow runs, or
                // mashed right after it, does not end the next turn (player.requestEndTurn's mash window).
                OnEndTurn = () => { if (PlayerCore != null) PlayerCore.RequestEndTurn(queue: true, press: true); },
                OnSpeed = () => { if (PlayerCore != null) PlayerCore.CycleSpeed(); },
                OnJournal = () => Overlays.ToggleJournal(),
                OnSettings = () => Overlays.OpenSettings(),
                // battle-visual-v1 §4.1 (review 19): no gear, unless the app asks for one (ctx.settingsInBattle).
                SettingsInBattle = BattleOverlays.SettingsInBattleOf(ctx),
                OnDrawer = open =>
                {
                    // battle_ui_ux_v2 §3.5: while the pile list is open the cards lift on hover but are not taken.
                    if (open) HandView.LetGo();
                    Overlays.SyncPause();
                    Overlays.UpdateShift();
                },
                // §2.2.2: hovering an enemy's omen badge keeps only that enemy's aimed cells lit.
                OnOmenHover = unit => FieldModule.FocusAim(unit),
            });
            // js: scenes/battle/BattleScene.js:173
            HandView = Hand.Create(env, new HandOpts
            {
                OnRelease = (id, zone, unit) => PlayerCore?.HandCallbacks.OnRelease?.Invoke(id, zone, unit),
                OnHover = face => PlayerCore?.HandCallbacks.OnHover?.Invoke(face),
                OnHoldStart = (face, unit) => PlayerCore?.HandCallbacks.OnHoldStart?.Invoke(face, unit),
                OnHoldEnd = face => PlayerCore?.HandCallbacks.OnHoldEnd?.Invoke(face),
                OnTarget = (face, unit) => PlayerCore?.HandCallbacks.OnTarget?.Invoke(face, unit),
                Preview = (id, unit) => PlayerCore != null && PlayerCore.HandCallbacks.Preview != null ? PlayerCore.HandCallbacks.Preview(id, unit) ?? "" : "",
                // §1.4 / §3.4: the enemy a Tab at rest chose (the floor's N) is where a selection starts (−1: the card's own target).
                RestingUnit = () => PlayerCore != null ? PlayerCore.RestingUnit : -1,
            });
            // js: scenes/battle/BattleScene.js:183
            PlayerCore = new BattlePlayer(env, HandView, FieldModule, HudView, BattleScene.FlowCtxOf(ctx, env));

            // ---- overlays: the journal (J / 手記) and the settings (Esc) ----
            Overlays = new BattleOverlays(this);
            Overlays.Attach();

            // ---- keys, pointer, fast-forward ----
            Input = new BattleInput(this);
            Input.Attach();

            // ---- the opening ----
            // js: scenes/battle/BattleScene.js:495
            foreach (BattleLayer name in BattleLayers.Order)
                if (name != BattleLayer.Backdrop && name != BattleLayer.Over) _opening.Add(name);
            foreach (BattleLayer name in _opening)
            {
                env.Layer.Node(name).Opacity = 0f;
                SetHidden(name, true);
            }
            // js: scenes/battle/BattleScene.js:509
            Promise.Race(BattleScene.FontsFor(ctx.Source), env.Real.Wait(L.OpenFontWaitMs)).OnSettled(Go);
        }

        /// <summary>visibility: hidden — laid out, unseen, taking no pointer.</summary>
        private void SetHidden(BattleLayer name, bool hidden)
        {
            CanvasGroup g = Env.Layer.Group(name);
            if (!g) return;
            g.blocksRaycasts = !hidden;
            g.interactable = !hidden;
        }

        /// <summary>
        /// go(): fonts first, so the card text measures right; then the battle. Until the opening frame is applied, only the
        /// layer's backdrop shows; then everything but the backdrop fades in together (L.OpenFadeMs, out). The over layer stays
        /// as it is: what opens there opens over the backdrop.
        /// </summary>
        // js: scenes/battle/BattleScene.js:498
        private void Go()
        {
            if (_started || Destroyed) return;
            _started = true;
            PlayerCore.Start().Forget();
            foreach (BattleLayer name in _opening) SetHidden(name, false);
            Env.Clock.Run(L.OpenFadeMs, Ease.Out, (t, _) =>
            {
                if (Destroyed) return;
                float a = t >= 1f ? 1f : (float)Math.Round(t, 3, MidpointRounding.AwayFromZero);
                foreach (BattleLayer name in _opening) Env.Layer.Node(name).Opacity = a;
            });
        }

        /// <summary>One frame (BattleSceneDriver): the input of the frame, then the clock (its tweens, runs, waits, the modules' and the player's OnFrame, the microtask drain).</summary>
        internal void Frame(float dtRealMs)
        {
            if (Destroyed) return;
            Microtasks.Drain();
            KeyStack.Pump();
            if (Destroyed) return;
            Input.PumpPointer();
            if (Destroyed) return;
            Env.Clock.Step(dtRealMs);
            Microtasks.Drain();
        }
    }

    // js: scenes/battle/BattleScene.js
    public static class BattleScene
    {
        /// <summary>JOURNAL_SHARE: battle_ui_ux_v2 §2.4, the journal drawer takes the right 30 % of the screen.</summary>
        // js: scenes/battle/BattleScene.js:23
        public const float JournalShare = 0.3f;

        /// <summary>LAYERS: docs/BATTLE_SCENE.md §2.1, bottom → top.</summary>
        // js: scenes/battle/BattleScene.js:26
        public static IReadOnlyList<BattleLayer> Layers => BattleLayers.Order;

        /// <summary>
        /// createBattleEnv(host, ctx): the scene root, its layers and the shared env (docs/BATTLE_SCENE.md §2), without any
        /// module. Public so a module's own dev scene can mount that module alone on the same env (it then steps env.Clock).
        /// </summary>
        // js: scenes/battle/BattleScene.js:54
        public static BattleViewEnv CreateBattleEnv(RectTransform host, BattleSceneCtx ctx)
        {
            if (!host) throw new ArgumentNullException(nameof(host));
            ctx = ctx ?? new BattleSceneCtx();
            // ctx.settings || {}: without the app's object the scene keeps one of its own (the defaults), which the speed
            // switch writes and onSettingsChange hands on, as the JS {} does.
            IBattleSettings settings = new TrialBattleSettings(ctx.Settings ?? new TrialSettings());
            ITrialAudio audio = ctx.Audio;
            IBattleAudio battleAudio = audio == null ? null : BattleAudio.Of((name, delayMs, mult) => audio.Play(name, delayMs, mult));
            IBattleLog log = ctx.Log ?? BattleLog.Of((message, error) =>
            {
                if (error != null && message != null) Debug.LogError(message + " " + error);
                else if (error != null) Debug.LogException(error);
                else if (message != null) Debug.LogError(message);
            });
            var env = new BattleViewEnv(settings, battleAudio, ctx.Layer, ctx.NodeKind, ctx.MiasmaPercent, log, ctx.Stage);
            env.BuildRoot(host);
            return env;
        }

        // js: scenes/battle/BattleScene.js:130
        /// <summary>
        /// mountBattleScene(host, ctx): the root and its layers under host, the env, the field, the hud, the hand and the
        /// player, the overlays, the keys and the pointer; the battle starts once the fonts are in (at most
        /// L.OpenFontWaitMs). The handle's Destroy tears it all down.
        /// </summary>
        public static BattleSceneHandle MountBattleScene(RectTransform host, BattleSceneCtx ctx)
        {
            if (ctx == null || ctx.Source == null) throw new ArgumentException("mountBattleScene: ctx.source is required");
            BattleViewEnv env = CreateBattleEnv(host, ctx);
            var scene = new BattleSceneHandle(ctx, env);
            scene.Mount();
            BattleSceneDriver driver = env.Root.gameObject.AddComponent<BattleSceneDriver>();
            driver.Scene = scene;
            scene.Driver = driver;
            Microtasks.Drain();
            return scene;
        }

        /// <summary>The engine-free ctx the player reads (player.js reads Source, Chain, Auto, OnFinished, OnError, OnSettingsChange).</summary>
        internal static BattleCtx FlowCtxOf(BattleSceneCtx ctx, BattleViewEnv env)
        {
            ITrialAudio audio = ctx.Audio;
            return new BattleCtx
            {
                Source = ctx.Source,
                Settings = env.Settings,
                Audio = audio == null ? null : BattleAudio.Of((name, delayMs, mult) => audio.Play(name, delayMs, mult)),
                Layer = ctx.Layer,
                LayerTitle = ctx.LayerTitle ?? "",
                NodeKind = ctx.NodeKind,
                MiasmaPercent = ctx.MiasmaPercent,
                Chain = ctx.Chain,
                DisclosureOf = ctx.DisclosureOf,
                OpenJournal = ctx.OpenJournal == null ? null : (Func<object, BattleJournalOpts, IDisposable>)((host, o) =>
                {
                    IOverlay overlay = ctx.OpenJournal(host as RectTransform, o);
                    return overlay == null ? null : new OverlayDisposable(overlay);
                }),
                Stage = ctx.Stage,
                SettingsInBattle = ctx.SettingsInBattle,
                OnSettings = ctx.OnSettings,
                OnSettingsChange = ctx.OnSettingsChange == null ? null
                    : (Action<IBattleSettings>)(s => ctx.OnSettingsChange((s as TrialBattleSettings)?.Settings ?? ctx.Settings)),
                OnError = ctx.OnError,
                OnFinished = ctx.OnFinished,
                Auto = ctx.Auto,
            };
        }

        private sealed class OverlayDisposable : IDisposable
        {
            private readonly IOverlay _overlay;
            public OverlayDisposable(IOverlay overlay) { _overlay = overlay; }
            public void Dispose() => _overlay.Destroy();
        }

        // ---- the fonts the cards are measured in ------------------------------------------------------------

        /// <summary>
        /// fontsFor(source): the web fonts the cards' text is measured in (review fix). The browser fetches a Japanese face's
        /// unicode-range subsets as they are laid out; TMP's dynamic atlases add glyphs on first use the same way, so the
        /// opening frame's pile names are rasterised into the display face (700, where hand-card.js fits the names) and any
        /// other hand text into the body face (400) before the battle starts. Synchronous here: the promise is resolved.
        /// </summary>
        // js: scenes/battle/BattleScene.js:108
        internal static Promise FontsFor(IDepictionSource source)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var rest = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                DepictionFrame frame = source.Frame;
                // JSON.stringify(frame.Piles, …): every 'Name' string.
                Walk(frame.Piles, (key, value) => { if (key == "Name") names.Add(value); }, 0);
                // JSON.stringify(frame.Hand, …): 'Name' strings to names, every other string to rest.
                Walk(frame.Hand, (key, value) => (key == "Name" ? names : rest).Add(value), 0);
            }
            catch (Exception)
            {
                // nothing to read: only the fonts already loading are waited for
            }
            try
            {
                TrialFonts.Prewarm(Join(names), Family.Display, 700);
                TrialFonts.Prewarm(Join(rest), Family.Body, 400);
            }
            catch (Exception)
            {
                // a font that fails to load leaves the fallbacks; the battle starts all the same
            }
            return Promise.Resolved();
        }

        private static string Join(HashSet<string> set)
        {
            var sb = new StringBuilder();
            foreach (string s in set) sb.Append(s);
            return sb.ToString();
        }

        /// <summary>JSON.stringify's walk: every string reached through public fields and lists, with the key it sits under.</summary>
        private static void Walk(object value, Action<string, string> onString, int depth, string key = null)
        {
            if (value == null || depth > 6) return;
            if (value is string s)
            {
                if (key != null) onString(key, s);
                return;
            }
            if (value is IEnumerable list)
            {
                // JSON.stringify hands an array's items to the replacer under their index ("0", "1" …), not the array's key.
                int i = 0;
                foreach (object item in list) Walk(item, onString, depth + 1, (i++).ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            Type t = value.GetType();
            if (t.IsPrimitive || t.IsEnum || value is decimal) return;
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v;
                try { v = f.GetValue(value); }
                catch (Exception) { continue; }
                Walk(v, onString, depth + 1, f.Name);
            }
        }
    }
}
