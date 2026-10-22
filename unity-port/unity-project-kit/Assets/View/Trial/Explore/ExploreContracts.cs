// What the router hands the exploration scene (the JS ctx of mountExploreScene, ExploreScene.js:12-22 and main.js:360
// toExplore; app.md §5.5 ExploreCtx), and what the scene hands back (lifeEnd's ending, the overlay request).
//
//   IScene scene = ExploreScene.Mount(host, new ExploreSceneCtx { Stage = stage, Audio = audio, Settings = settings,
//       Expedition = live.Expedition, Inheritance = new ExploreInheritance { TraceView = …, Take = …, Scribbles = … },
//       OnBattle = () => …, OnLifeEnd = end => …, OnOverlay = (kind, info) => { OpenOverlay(kind); return null; },
//       OnSaveQuit = …, OnPersist = …, OnCarving = … });
using System;
using System.Collections.Generic;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using Journal;
using Trial.Audio;
using Trial.Bridge;
using Trial.Kit;

namespace Trial.Explore
{
    /// <summary>ctx.onOverlay's kind: 'journal' | 'cards' | 'settings'.</summary>
    public static class ExploreOverlayKind
    {
        public const string Journal = "journal";
        public const string Cards = "cards";
        public const string Settings = "settings";
    }

    /// <summary>ctx.onOverlay's info: { book, page, screen } (ExploreScene.js:2202).</summary>
    public sealed class ExploreOverlayInfo
    {
        /// <summary>screen.Journal.</summary>
        public JournalBook Book { get; set; }

        /// <summary>screen.Cards for 'cards', screen.Settings for 'settings', else null.</summary>
        public OverlayPage Page { get; set; }

        public ExplorationScreen Screen { get; set; }
    }

    /// <summary>
    /// What ctx.inheritance.take returns (main.js:370): the lineage's take (Inheritance.TakeTrace(...).Taken) and what it
    /// did to this life (Expedition.Inherit's result, with the router's 走り書き rows: InheritResult.WithScribbles). Null
    /// when nothing was taken (another window moved on, the life is over, the trace refuses).
    /// </summary>
    public sealed class ExploreTaken
    {
        public ExploreTaken(TakenLegacy taken, InheritResult result)
        {
            Taken = taken;
            Result = result;
        }

        public TakenLegacy Taken { get; }

        /// <summary>The JS Taken.Result: the 段 and 開示度 this heir ends with (TraceGain writes the 受け取った遺産 card from it).</summary>
        public InheritResult Result { get; }

        /// <summary>taken.Lost: how many candidates were lost.</summary>
        public int Lost => Taken != null ? Taken.Lost : 0;
    }

    /// <summary>ctx.inheritance (ExploreScene.js:17): the lineage's side of a trace. Any member may be null (the JS checks each).</summary>
    public sealed class ExploreInheritance
    {
        /// <summary>traceView(layer, nodeId) → traceNodeView | null (main.js:366 reads the layer only).</summary>
        public Func<int, int, TraceNodeView> TraceView { get; set; }

        /// <summary>take(picks) → the take, or null when nothing was taken (main.js:370).</summary>
        public Func<IReadOnlyList<string>, ExploreTaken> Take { get; set; }

        /// <summary>scribbles() → the 走り書き the life carries now (a copy).</summary>
        public Func<IReadOnlyList<Scribble>> Scribbles { get; set; }
    }

    /// <summary>
    /// What ctx.onLifeEnd receives (ExploreScene.js:165 lifeEnd): the phase the life ended in (MiasmaDeath / Fallen /
    /// Completed / Survived), the end card the builder wrote, and where it happened.
    /// </summary>
    public sealed class ExploreLifeEnd
    {
        public RunPhase Ending { get; set; }
        public ScreenCard Card { get; set; }
        public string Title { get; set; }
        public IReadOnlyList<string> Lines { get; set; }
        public int Layer { get; set; }
        public string LayerTitle { get; set; }
        public int NodeId { get; set; }
        public NodeKind NodeKind { get; set; }
        public ExplorationState State { get; set; }
    }

    /// <summary>
    /// The JS ctx of mountExploreScene (ExploreScene.js:12-22). Everything but Expedition may be left out, as in the JS:
    /// no OnBattle shows the stand-in battle card, no OnOverlay the scene's own fallback overlays, no OnLifeEnd starts a
    /// new life in place.
    /// </summary>
    public sealed class ExploreSceneCtx
    {
        /// <summary>ctx.stage: the shared stage (the scene's clock runs on its ClockDriver).</summary>
        public TrialStage Stage { get; set; }

        /// <summary>ctx.audio (optional).</summary>
        public ITrialAudio Audio { get; set; }

        /// <summary>ctx.settings: 演出短縮 (ShortFx) is read live (optional: the defaults).</summary>
        public TrialSettings Settings { get; set; }

        /// <summary>
        /// prefers-reduced-motion (ExploreScene.js:186): null reads the kit's switch (CssTime.ReducedMotion) at mount.
        /// </summary>
        public bool? ReducedMotion { get; set; }

        /// <summary>ctx.expedition: the recorded Expedition (its Raw for reads the facade does not pass on).</summary>
        public RecordedExpedition Expedition { get; set; }

        /// <summary>ctx.inheritance, or null.</summary>
        public ExploreInheritance Inheritance { get; set; }

        /// <summary>ctx.onBattle(expedition) → Promise: runs the fights of the waiting encounter; resolves when they are over.</summary>
        public Func<Promise> OnBattle { get; set; }

        /// <summary>ctx.onLifeEnd(ending): the life is over (the app shows the ending).</summary>
        public Action<ExploreLifeEnd> OnLifeEnd { get; set; }

        /// <summary>
        /// ctx.onOverlay(kind, info) → Promise | null: the app mounts the overlay. main.js returns nothing, so the scene does
        /// not wait; a promise returned is awaited (and the journal's close sound plays after it).
        /// </summary>
        public Func<string, ExploreOverlayInfo, Promise> OnOverlay { get; set; }

        /// <summary>ctx.onSaveQuit(): only the fallback settings page uses it.</summary>
        public Action OnSaveQuit { get; set; }

        /// <summary>ctx.onPersist(): the app saves the life (save by replay).</summary>
        public Action OnPersist { get; set; }

        /// <summary>ctx.onCarving(): a fresh 彫刻 was read; the lineage keeps it.</summary>
        public Action OnCarving { get; set; }
    }
}
