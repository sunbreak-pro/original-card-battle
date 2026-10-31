// The battle scene's shared environment `env` (docs/BATTLE_SCENE.md §2), engine-free: the part of
// scenes/battle/BattleScene.js createBattleEnv (:54-98) that holds no DOM — the settings, the clocks, the effect
// switches facade, the geometry registry, the safe sound, the layer number, the node kind, the miasma and the log —
// plus the geometry types the registry hands around. The Unity scene derives its view env from BattleEnv and adds the
// root, the eleven layers and toStage.
//
// Stage coordinates are 1920×1080 px, origin top left, y down, as in the JS. A unit argument of −1 stands for the JS
// `undefined` (one enemy / "the frame's focus enemy"); never pass 0 where the JS passes undefined.
using System;
using System.Collections.Generic;
using Depiction;

namespace Trial.Battle
{
    // ---- settings ---------------------------------------------------------------------------------

    /// <summary>
    /// The app's settings object the battle reads live (JS ctx.settings): `speed` (1 | 1.25 | 1.5, written by the
    /// player's speed switch) and `shortFx` (演出短縮). The app's own settings type is adapted to this (Trial.Bridge
    /// cannot see this assembly).
    /// </summary>
    public interface IBattleSettings
    {
        /// <summary>
        /// settings.speed: 1 | 1.25 | 1.5 (BattleClock.StepOfSpeed reads anything else as ×1.25). A double, as the JS number
        /// and the app's stored value are: StepOfSpeed compares it exactly (`===`).
        /// </summary>
        double Speed { get; set; }

        /// <summary>settings.shortFx: 演出短縮 — every scene tween and wait at L.ShortScale.</summary>
        bool ShortFx { get; }
    }

    /// <summary>A plain settings object (JS `ctx.settings || {}` and the dev / test pages').</summary>
    public sealed class BattleSettings : IBattleSettings
    {
        /// <summary>settings.speed; the default is BattleSpeed.Default's (×1.25; the JS `{}` has none, which reads the same).</summary>
        public double Speed { get; set; } = 1.25;

        /// <summary>settings.shortFx.</summary>
        public bool ShortFx { get; set; }
    }

    /// <summary>
    /// The app's own settings (Trial.Bridge.TrialSettings, main.js `settings`) as the battle reads them: live, and the speed
    /// switch writes through to them (the app then persists them in ctx.onSettingsChange).
    /// </summary>
    public sealed class TrialBattleSettings : IBattleSettings
    {
        public TrialBattleSettings(Trial.Bridge.TrialSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>The app's settings object.</summary>
        public Trial.Bridge.TrialSettings Settings { get; }

        /// <summary>settings.speed, the stored double itself (no narrowing: a stored 1.0000000001 stays off the ×1 step, as in the JS).</summary>
        public double Speed
        {
            get => Settings.Speed;
            set => Settings.Speed = value;
        }

        /// <summary>settings.shortFx.</summary>
        public bool ShortFx => Settings.ShortFx;
    }

    // ---- sound ------------------------------------------------------------------------------------

    /// <summary>
    /// ctx.audio as the battle uses it: `audio.play(name, delayMs, mult)` (docs/AUDIO.md). The app's TrialAudio is adapted
    /// to it (<see cref="BattleAudio.Of"/>).
    /// </summary>
    public interface IBattleAudio
    {
        /// <param name="name">The sound's preset name ('hitSlash', 'draw', …).</param>
        /// <param name="delayMs">Real ms to wait before it sounds (the player's heartbeat ×3 at 0 / 900 / 1800).</param>
        /// <param name="mult">A volume factor; null (JS undefined) plays the preset's own.</param>
        void Play(string name, float delayMs, float? mult);
    }

    /// <summary>Adapters to <see cref="IBattleAudio"/>.</summary>
    public static class BattleAudio
    {
        /// <summary>An IBattleAudio over a function (play(name, delayMs, mult)).</summary>
        public static IBattleAudio Of(Action<string, float, float?> play) => play == null ? null : new FnAudio(play);

        private sealed class FnAudio : IBattleAudio
        {
            private readonly Action<string, float, float?> _play;
            public FnAudio(Action<string, float, float?> play) { _play = play; }
            public void Play(string name, float delayMs, float? mult) => _play(name, delayMs, mult);
        }
    }

    /// <summary>
    /// The scene's sound (`env.audio`): never throws. While fast-forward is on (the clock's FastForward), a sound of the
    /// same name asked for again within L.FfSoundGapMs of real time is dropped (review fix: with every stagger collapsed, a
    /// turn's 6 discards and 6 draws fired in the same instant and rang louder than a hit).
    /// </summary>
    // js: scenes/battle/BattleScene.js:33 (safeAudio)
    public sealed class SafeAudio
    {
        private readonly IBattleAudio _audio;
        private readonly BattleClock _clocks;
        private readonly Dictionary<string, float> _last = new Dictionary<string, float>(StringComparer.Ordinal);

        /// <param name="audio">ctx.audio; null makes every play a no-op.</param>
        /// <param name="clocks">The battle's clocks (the fast-forward flag and real time); null keeps no gap.</param>
        public SafeAudio(IBattleAudio audio, BattleClock clocks)
        {
            _audio = audio;
            _clocks = clocks;
        }

        /// <summary>
        /// play(name, delayMs, mult): plays `name` after `delayMs` real ms (NaN reads 0, as `delayMs || 0`). No sound, an empty
        /// name, or a throwing player: nothing happens. Under fast-forward a repeat of the same name whose start lands
        /// within L.FfSoundGapMs real ms of the last one's is dropped; every play (fast-forward or not) is remembered.
        /// </summary>
        // js: scenes/battle/BattleScene.js:36
        public void Play(string name, float delayMs = 0f, float? mult = null)
        {
            if (_audio == null || string.IsNullOrEmpty(name)) return;
            float delay = float.IsNaN(delayMs) ? 0f : delayMs;
            if (_clocks != null && _clocks.FastForward)
            {
                float at = _clocks.Real.Now + delay;
                if (_last.TryGetValue(name, out float prev) && Math.Abs(at - prev) < L.FfSoundGapMs) return;
                _last[name] = at;
            }
            else if (_clocks != null) _last[name] = _clocks.Real.Now + delay;
            try { _audio.Play(name, delay, mult); }
            catch (Exception) { /* sound never stops the battle */ }
        }
    }

    // ---- log --------------------------------------------------------------------------------------

    /// <summary>
    /// Where the scene's console.error goes (Unity: Debug.LogError / Debug.LogException). One call per JS console.error:
    /// a message, an error, or both (`console.error('[battle] the source failed; …:', err)`).
    /// </summary>
    public interface IBattleLog
    {
        void Error(string message, Exception error = null);
    }

    /// <summary>Ready-made logs.</summary>
    public static class BattleLog
    {
        /// <summary>Writes to the standard error stream (the engine-free default; tests and dotnet tools).</summary>
        public static readonly IBattleLog Console = new FnLog((m, e) =>
        {
            if (m != null && e != null) System.Console.Error.WriteLine(m + " " + e);
            else if (m != null) System.Console.Error.WriteLine(m);
            else if (e != null) System.Console.Error.WriteLine(e);
        });

        /// <summary>A log over a function (message, error) — either may be null.</summary>
        public static IBattleLog Of(Action<string, Exception> error) => error == null ? Console : new FnLog(error);

        private sealed class FnLog : IBattleLog
        {
            private readonly Action<string, Exception> _error;
            public FnLog(Action<string, Exception> error) { _error = error; }
            public void Error(string message, Exception error = null) => _error(message, error);
        }
    }

    // ---- effect switches --------------------------------------------------------------------------

    /// <summary>
    /// `env.fx`: a facade over the ported EffectSwitches (everything on; the scene has no Inspector). The JS takes string
    /// ids (`env.fx.ms('CardHover')`); C# takes EffectId. A module that animates a beat uses the ms the player hands it; a
    /// module that animates something of its own (hover, grab, dim, snap, receiver fade) takes its length from Ms(id).
    /// </summary>
    // js: scenes/battle/BattleScene.js:74
    public sealed class BattleFx
    {
        public BattleFx(EffectSwitches switches)
        {
            Switches = switches ?? throw new ArgumentNullException(nameof(switches));
        }

        /// <summary>fx.switches: the EffectSwitches itself (for EffectPlan.DrawCardStartMs / RebuildStartMs / WaitMs).</summary>
        // js: scenes/battle/BattleScene.js:75
        public EffectSwitches Switches { get; }

        /// <summary>fx.on(id).</summary>
        // js: scenes/battle/BattleScene.js:76
        public bool On(EffectId id) => Switches.IsOn(id);

        /// <summary>fx.ms(id): one item's length, 0 when off.</summary>
        // js: scenes/battle/BattleScene.js:77
        public float Ms(EffectId id) => Switches.Ms(id);

        /// <summary>fx.stagger(id): the gap between items, 0 when off.</summary>
        // js: scenes/battle/BattleScene.js:78
        public float Stagger(EffectId id) => Switches.StaggerMs(id);

        /// <summary>fx.wait(id, count = 1): how long the flow waits for `count` items.</summary>
        // js: scenes/battle/BattleScene.js:79
        public float Wait(EffectId id, int count = 1) => Switches.WaitMs(id, count);
    }

    // ---- node kind and layers ---------------------------------------------------------------------

    /// <summary>env.nodeKind: what kind of node the fight is ('Battle' | 'Elite' | 'Boss').</summary>
    public enum BattleNodeKind
    {
        Battle,
        Elite,
        Boss,
    }

    /// <summary>
    /// The scene's eleven full-stage layers, bottom → top (BattleScene.js LAYERS, docs/BATTLE_SCENE.md §2.1). The CSS
    /// z-index of each is <see cref="BattleLayers.CssZ"/>; every layer takes no pointer events but its interactive pieces.
    /// </summary>
    public enum BattleLayer
    {
        /// <summary>z 0, field: per-layer sky / haze / motes, the horizon at y 556, dark earth below.</summary>
        Backdrop,
        /// <summary>z 10, field: floor tiles, gap chip, foot lights, figures.</summary>
        Field,
        /// <summary>z 15, field (+ the hand's target hooks): strike shapes, bursts, marks, shield, ghosts, rebuild sheet.</summary>
        Fx,
        /// <summary>z 20, hud: status panels, omen badges (+ plans), stance lists.</summary>
        Panels,
        /// <summary>z 30, hand / field: throw line, the invisible receiver, 俊敏's choice marks.</summary>
        Marks,
        /// <summary>z 40, hud (+ the hand's guide line): corner, speed, journal, stamina tag, piles, played, reserve hint, end turn.</summary>
        Hud,
        /// <summary>z 50, field: the enemy-turn shade below y 790.</summary>
        Shade,
        /// <summary>z 60, hand: the fan.</summary>
        Hand,
        /// <summary>z 70, hand: the held card.</summary>
        Held,
        /// <summary>z 80, field / hand / hud: prediction tag, floating numbers, banners, refusal line.</summary>
        Float,
        /// <summary>z 90, hud / hand / scene: tooltips, card detail, pile drawer, result card, journal overlay.</summary>
        Over,
    }

    /// <summary>The layers' order and z (battle.css:28-38).</summary>
    public static class BattleLayers
    {
        /// <summary>LAYERS, bottom → top.</summary>
        // js: scenes/battle/BattleScene.js:26
        public static readonly IReadOnlyList<BattleLayer> Order = Array.AsReadOnly(new[]
        {
            BattleLayer.Backdrop, BattleLayer.Field, BattleLayer.Fx, BattleLayer.Panels, BattleLayer.Marks, BattleLayer.Hud,
            BattleLayer.Shade, BattleLayer.Hand, BattleLayer.Held, BattleLayer.Float, BattleLayer.Over,
        });

        /// <summary>The CSS z-index of a layer (battle.css: 0, 10, 15, 20, 30, 40, 50, 60, 70, 80, 90).</summary>
        public static int CssZ(BattleLayer layer)
        {
            switch (layer)
            {
                case BattleLayer.Backdrop: return 0;
                case BattleLayer.Field: return 10;
                case BattleLayer.Fx: return 15;
                case BattleLayer.Panels: return 20;
                case BattleLayer.Marks: return 30;
                case BattleLayer.Hud: return 40;
                case BattleLayer.Shade: return 50;
                case BattleLayer.Hand: return 60;
                case BattleLayer.Held: return 70;
                case BattleLayer.Float: return 80;
                case BattleLayer.Over: return 90;
                default: throw new ArgumentOutOfRangeException(nameof(layer), layer, "No such layer.");
            }
        }
    }

    // ---- geometry types ---------------------------------------------------------------------------

    /// <summary>A stage point {x, y} (stage px, y down).</summary>
    public readonly struct StagePoint
    {
        public readonly float X;
        public readonly float Y;

        public StagePoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => "(" + X + ", " + Y + ")";
    }

    /// <summary>A stage box {x, y, w, h}: left and top edges, width and height (stage px, y down).</summary>
    public readonly struct StageBox
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;

        public StageBox(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        /// <summary>x + w.</summary>
        public float Right => X + W;

        /// <summary>y + h.</summary>
        public float Bottom => Y + H;

        public override string ToString() => "(" + X + ", " + Y + ", " + W + " x " + H + ")";
    }

    /// <summary>
    /// A number panel's box (status.js box(): {x, y, w, h, chipsW}) — `chipsW` the width its status chips take (0 with
    /// none; the hand's hover line counts them four times, StatusBarView's chips being left-aligned).
    /// </summary>
    public readonly struct PanelBox
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;
        public readonly float ChipsW;

        public PanelBox(float x, float y, float w, float h, float chipsW)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
            ChipsW = chipsW;
        }

        /// <summary>The box without the chips' width.</summary>
        public StageBox Box => new StageBox(X, Y, W, H);
    }

    /// <summary>
    /// geom.figure(side, unit) (field.js:1014): a figure as it stands now. x includes lunges and walks; HomeX is its place
    /// between motions — already the new cell during a floor walk, so Walking says to follow X then; Width is the body
    /// width the target hooks frame; ArtX0–ArtX1 the art's own box across (wider for a tail or a spear; review 21).
    /// </summary>
    public sealed class FigureGeom
    {
        /// <summary>x: the body's middle now (the arena's slide included).</summary>
        public float X;
        /// <summary>feetY: L.FeetY + the figure's own y (a rise or a fall's sink).</summary>
        public float FeetY;
        /// <summary>height: FigureFrame.HeightFor(size).</summary>
        public float Height;
        /// <summary>width: the body width.</summary>
        public float Width;
        /// <summary>homeX: its place between motions.</summary>
        public float HomeX;
        /// <summary>top: feetY − height.</summary>
        public float Top;
        /// <summary>artX0: the art's left edge.</summary>
        public float ArtX0;
        /// <summary>artX1: the art's right edge.</summary>
        public float ArtX1;
        /// <summary>chest: L.Chest (65 %) of the height up from the feet.</summary>
        public StagePoint Chest;
        /// <summary>head: L.Head (84 %) of the height up from the feet.</summary>
        public StagePoint Head;
        /// <summary>unit: the enemy's unit; −1 for the player.</summary>
        public int Unit = -1;
        /// <summary>down: fallen and gone.</summary>
        public bool Down;
        /// <summary>walking: it walks on the floor (MoveOnFloor): what follows it rides with X, not HomeX.</summary>
        public bool Walking;
    }

    /// <summary>geom.receivers() row: { unit, box } — one standing enemy's receiver, cut at the middle between neighbours' homes.</summary>
    public readonly struct ReceiverBox
    {
        public readonly int Unit;
        public readonly ScreenBox Box;

        public ReceiverBox(int unit, ScreenBox box)
        {
            Unit = unit;
            Box = box;
        }
    }

    /// <summary>geom.freeStepBoxes() row: { x, y, w, h, dir } — one of 俊敏's choice marks as it stands (dir −1 / 0 / +1).</summary>
    public readonly struct FreeStepBox
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;
        public readonly int Dir;

        public FreeStepBox(float x, float y, float w, float h, int dir)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
            Dir = dir;
        }

        public StageBox Box => new StageBox(X, Y, W, H);
    }

    /// <summary>geom.statusBoxes() row: { side, unit, box } — a number panel shown (the player's unit is −1).</summary>
    public readonly struct StatusPanelBox
    {
        public readonly UnitSide Side;
        public readonly int Unit;
        public readonly PanelBox Box;

        public StatusPanelBox(UnitSide side, int unit, PanelBox box)
        {
            Side = side;
            Unit = unit;
            Box = box;
        }
    }

    // ---- the geometry registry --------------------------------------------------------------------

    /// <summary>
    /// `env.geom` (docs/BATTLE_SCENE.md §2.4): cross-module positions in stage px, read live (they follow motion). Each
    /// module fills its own slots when it is created; the others call them lazily, never at construction time. An empty
    /// slot is the JS's missing getter: ask with `?.Invoke(…)` and take the JS's default answer (`[]`, null, false, 0).
    /// </summary>
    public sealed class BattleGeom
    {
        // ---- field (field.js:1014-1061) ----

        /// <summary>
        /// geom.figure(side, unit): `side`'s figure now (unit −1: the player, or the frame's focus enemy); null for an
        /// enemy the frame never had.
        /// </summary>
        // js: scenes/battle/field.js:1014
        public Func<UnitSide, int, FigureGeom> Figure { get; set; }

        /// <summary>
        /// geom.receiver(unit): the invisible 340×340 receiver over enemy `unit`'s home (−1: the focus enemy), its middle on
        /// the body's middle, kept 16 px inside the stage; null without that enemy. ScreenBox-shaped for ReceiverDrop.Takes.
        /// </summary>
        // js: scenes/battle/field.js:1034
        public Func<int, ScreenBox?> Receiver { get; set; }

        /// <summary>geom.receivers(): one receiver per standing enemy, left to right, each cut at the middle between two neighbours' homes.</summary>
        // js: scenes/battle/field.js:1042
        public Func<IReadOnlyList<ReceiverBox>> Receivers { get; set; }

        /// <summary>geom.freeStepBoxes(): 俊敏's choice marks as they stand (the arena's slide included); empty while none shows.</summary>
        // js: scenes/battle/field.js:1054
        public Func<IReadOnlyList<FreeStepBox>> FreeStepBoxes { get; set; }

        /// <summary>geom.cellBox(cell): floor tile `cell`'s box (the arena's slide included), or null.</summary>
        // js: scenes/battle/field.js:1055
        public Func<int, StageBox?> CellBox { get; set; }

        // ---- hud (hud.js:666-694) ----

        /// <summary>geom.pileMiddle(kind): the middle of the draw / discard / exile pile (PilesView.MiddleOf).</summary>
        // js: scenes/battle/hud.js:666
        public Func<PileKind, StagePoint> PileMiddle { get; set; }

        /// <summary>geom.omenBox(unit): enemy `unit`'s omen badge box (−1: the leader's).</summary>
        // js: scenes/battle/hud.js:667
        public Func<int, StageBox> OmenBox { get; set; }

        /// <summary>geom.omenShown(unit): whether enemy `unit`'s badge shows now (a spent or hidden badge is no part of the enemy to click).</summary>
        // js: scenes/battle/hud.js:669
        public Func<int, bool> OmenShown { get; set; }

        /// <summary>geom.omenBoxes(): the badge's place over every standing enemy, shown or between two omens (the floats keep under them).</summary>
        // js: scenes/battle/hud.js:674
        public Func<IReadOnlyList<StageBox>> OmenBoxes { get; set; }

        /// <summary>geom.endTurnBox(): the end-turn plate (L.EndTurn).</summary>
        // js: scenes/battle/hud.js:675
        public Func<StageBox> EndTurnBox { get; set; }

        /// <summary>
        /// geom.statusBox(side, unit): a number panel's box as it stands, or null when not shown. Asked every frame, so it is
        /// computed from the layout numbers (StatusPanelLayout), never read from the hierarchy.
        /// </summary>
        // js: scenes/battle/hud.js:677
        public Func<UnitSide, int, PanelBox?> StatusBox { get; set; }

        /// <summary>geom.statusBoxes(): every number panel shown (a click on an enemy's panel plays a selected card on it; the hover line keeps off them).</summary>
        // js: scenes/battle/hud.js:682
        public Func<IReadOnlyList<StatusPanelBox>> StatusBoxes { get; set; }

        /// <summary>geom.stanceBox(side): the stance list's box over every frame it shows, at its rest place or its high place; null while hidden.</summary>
        // js: scenes/battle/hud.js:689
        public Func<UnitSide, StageBox?> StanceBox { get; set; }

        /// <summary>geom.stanceSlot(side): the slot at the end of `side`'s stance list where a stance just put down goes (§5.11 順 1); null while hidden.</summary>
        // js: scenes/battle/hud.js:691
        public Func<UnitSide, StageBox?> StanceSlot { get; set; }

        /// <summary>geom.coversPointer(pt): an open pile drawer covers `pt` (the hand then does not hover through it).</summary>
        // js: scenes/battle/hud.js:692
        public Func<StagePoint, bool> CoversPointer { get; set; }

        /// <summary>geom.drawerRight(): the pile list drawer's right edge as it slides (0 closed).</summary>
        // js: scenes/battle/hud.js:694
        public Func<float> DrawerRight { get; set; }

        // ---- hand (hand.js:1223) ----

        /// <summary>geom.handCard(cardId): the centre of that card now (in the fan or leaving it), or null.</summary>
        // js: scenes/battle/hand.js:1223
        public Func<string, StagePoint?> HandCard { get; set; }

        // ---- scene (BattleScene.js:210-212) ----

        /// <summary>geom.journalCovers(pt): the journal's drawer (the right 30 %) covers `pt` while it is open.</summary>
        // js: scenes/battle/BattleScene.js:210
        public Func<StagePoint, bool> JournalCovers { get; set; }

        /// <summary>geom.journalEdge(): the journal drawer's left edge (1344) while it is open, else null.</summary>
        // js: scenes/battle/BattleScene.js:212
        public Func<float?> JournalEdge { get; set; }
    }

    // ---- env ---------------------------------------------------------------------------------------

    /// <summary>
    /// `env` without its DOM (docs/BATTLE_SCENE.md §2): what the player and the three modules share. The Unity scene derives
    /// its view env from it (the root, the eleven layers, toStage). OCB and L are static classes here (the kit, <see cref="L"/>).
    /// </summary>
    // js: scenes/battle/BattleScene.js:54 (createBattleEnv)
    public class BattleEnv
    {
        /// <param name="settings">ctx.settings, read live; null = a fresh <see cref="BattleSettings"/> (JS `ctx.settings || {}`).</param>
        /// <param name="audio">ctx.audio; null = silent.</param>
        /// <param name="layer">ctx.layer, the exploration's layer 1..7 (0 reads 1; clamped to 1..7).</param>
        /// <param name="nodeKind">ctx.nodeKind (JS default 'Battle').</param>
        /// <param name="miasmaPercent">ctx.miasmaPercent when the app gives it, else null (the writer's corner always says 0).</param>
        /// <param name="log">Where console.error goes; null = <see cref="BattleLog.Console"/>.</param>
        public BattleEnv(IBattleSettings settings = null, IBattleAudio audio = null, int layer = 1,
            BattleNodeKind nodeKind = BattleNodeKind.Battle, float? miasmaPercent = null, IBattleLog log = null)
        {
            Settings = settings ?? new BattleSettings();
            Clock = new BattleClock(Settings);
            Fx = new BattleFx(EffectSwitches.AllOn());
            Geom = new BattleGeom();
            Audio = new SafeAudio(audio, Clock);
            LayerNo = LayerNoOf(layer);
            NodeKind = nodeKind;
            MiasmaPercent = miasmaPercent;
            Log = log ?? BattleLog.Console;
        }

        /// <summary>env.clock: the scaled scene clock (§2.2). The scene destroys it with the battle (BattleClock.Destroy).</summary>
        public BattleClock Clock { get; }

        /// <summary>env.real: the unscaled clock.</summary>
        public BattleRealClock Real => Clock.Real;

        /// <summary>env.fx: the effect switches facade (§2.3).</summary>
        public BattleFx Fx { get; }

        /// <summary>env.geom: the geometry registry (§2.4).</summary>
        public BattleGeom Geom { get; }

        /// <summary>env.audio: the safe sound with the fast-forward gap.</summary>
        public SafeAudio Audio { get; }

        /// <summary>env.settings: ctx.settings (read speed / shortFx live; only the player writes speed).</summary>
        public IBattleSettings Settings { get; }

        /// <summary>env.layerNo: the exploration's layer, 1..7.</summary>
        public int LayerNo { get; }

        /// <summary>env.nodeKind.</summary>
        public BattleNodeKind NodeKind { get; }

        /// <summary>env.miasmaPercent: the exploration's miasma % when the app hands it, else null.</summary>
        public float? MiasmaPercent { get; }

        /// <summary>The scene's console.error.</summary>
        public IBattleLog Log { get; }

        /// <summary>`Math.max(1, Math.min(7, ctx.layer | 0 || 1))`.</summary>
        // js: scenes/battle/BattleScene.js:91
        public static int LayerNoOf(int layer) => Math.Max(1, Math.Min(7, layer == 0 ? 1 : layer));
    }
}
