// The trial's one-shot sounds as AudioClips. Every named sound is synthesised (Trial.Audio.Synth's PresetRenderer: the
// voice exactly as the JS builds it, its damp room baked in) on a background thread, in priority order, and turned into an
// AudioClip on the main thread a few per frame. A variant asked for before its render is ready is rendered on the spot.
//
// A pitch change is never a playback-rate change (fixed filters such as paper's high-pass and the step surfaces stay
// put), so the ±3 % alternation, the miasma lines' pitch and the volume multiplier are rendered variants (docs/AUDIO.md,
// audio.md §6.4).
// [deviation] The browser reads a different stretch of the noise buffer for every play; a cached variant always reads
// the same one (the play counters of its alternation class). The bursts are noise either way; the alternation, the
// audible part, is exact.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Trial.Audio.Synth;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Trial.Audio
{
    /// <summary>
    /// What makes one rendering of a sound differ from another: the name, the ±3 % alternation class
    /// (<c>turn % 3</c>; 1 for a timbre that does not alternate), the step's ground and pan, the volume multiplier and the
    /// pitch factor. The reverb is the cache's (a change re-renders every clip).
    /// </summary>
    public readonly struct SfxVariant : IEquatable<SfxVariant>
    {
        public readonly string Name;
        /// <summary>The alternation class: the play counter after its increment, mod 3 (wobble ((w − 1)·3 %)).</summary>
        public readonly int W;
        /// <summary>The footstep ground (step timbre only, else null).</summary>
        public readonly string Surface;
        /// <summary>The stereo pan of a meta voice (0 = none).</summary>
        public readonly double Pan;
        /// <summary>The volume multiplier as the synth applies it (battle: <c>mult || 1</c>; meta: <c>mult ?? 1</c>).</summary>
        public readonly double Mult;
        /// <summary>The pitch factor of a meta voice (<c>pitchMul || 1</c>).</summary>
        public readonly double PitchMul;
        /// <summary>A design-system (battle) preset, rendered by its own synth.</summary>
        public readonly bool Battle;

        private SfxVariant(string name, int w, string surface, double pan, double mult, double pitchMul, bool battle)
        {
            Name = name; W = w; Surface = surface; Pan = pan; Mult = mult; PitchMul = pitchMul; Battle = battle;
        }

        /// <summary>
        /// js: vendor/ds/bundle.js:826 S.play's variant: <paramref name="turn"/> is the design system's play counter after its
        /// increment; paper and wood alternate by it; <c>vol × (mult || 1)</c>.
        /// </summary>
        public static SfxVariant OfBattle(string name, string timbre, int turn, double? mult = null)
        {
            int w = SoundRules.Wobbles(timbre, false) ? Mod3(turn) : 1;
            double m = mult.HasValue && mult.Value != 0 && !double.IsNaN(mult.Value) ? mult.Value : 1;
            return new SfxVariant(name, w, null, 0, m, 1, true);
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:722-735 play's variant: <paramref name="turn"/> is the meta play counter after its increment;
        /// paper, wood and step alternate by it; <c>vol × (mult ?? 1)</c>; <c>pitch × (pitchMul || 1)</c>; the pan and ground as
        /// play resolved them (a step always has both).
        /// </summary>
        public static SfxVariant OfMeta(string name, string timbre, int turn, double? mult = null, double? pan = null, string surface = null, double? pitchMul = null)
        {
            int w = SoundRules.Wobbles(timbre, true) ? Mod3(turn) : 1;
            bool step = timbre == "step";
            double p = pan.HasValue && !double.IsNaN(pan.Value) ? pan.Value : 0;
            double pm = pitchMul.HasValue && pitchMul.Value != 0 && !double.IsNaN(pitchMul.Value) ? pitchMul.Value : 1;
            return new SfxVariant(name, w, step ? surface ?? "stone" : null, p, mult ?? 1, pm, false);
        }

        private static int Mod3(int turn) => ((turn % 3) + 3) % 3;

        /// <summary>
        /// The render request: the play counters set so the voice's own increment lands on this class (turn = w + 3k), the
        /// meta noise counter at 16·w (each class reads its own stretch of the noise).
        /// </summary>
        public SoundRequest ToRequest(double reverb) => new SoundRequest
        {
            Name = Name,
            Mult = Mult,
            Pan = Battle ? (double?)null : Pan,
            Surface = Surface,
            PitchMul = Battle ? (double?)null : PitchMul,
            Turn = (W + 2) % 3,
            NoiseTurn = Battle ? 0 : 16 * W,
            StepTurn = 0,
            Reverb = reverb,
        };

        public bool Equals(SfxVariant o) =>
            string.Equals(Name, o.Name, StringComparison.Ordinal) && W == o.W && string.Equals(Surface, o.Surface, StringComparison.Ordinal) &&
            Pan.Equals(o.Pan) && Mult.Equals(o.Mult) && PitchMul.Equals(o.PitchMul) && Battle == o.Battle;

        public override bool Equals(object obj) => obj is SfxVariant v && Equals(v);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0;
                h = h * 31 + W;
                h = h * 31 + (Surface != null ? StringComparer.Ordinal.GetHashCode(Surface) : 0);
                h = h * 31 + Pan.GetHashCode();
                h = h * 31 + Mult.GetHashCode();
                h = h * 31 + PitchMul.GetHashCode();
                return h;
            }
        }

        public override string ToString()
        {
            var s = Name + "#" + W.ToString(CultureInfo.InvariantCulture);
            if (Surface != null) s += "@" + Surface;
            if (Pan != 0) s += " pan " + Pan.ToString("R", CultureInfo.InvariantCulture);
            if (Mult != 1) s += " x" + Mult.ToString("R", CultureInfo.InvariantCulture);
            if (PitchMul != 1) s += " pitch " + PitchMul.ToString("R", CultureInfo.InvariantCulture);
            return s;
        }
    }

    /// <summary>
    /// Variant → AudioClip. Main thread: <see cref="Get"/>, <see cref="Enqueue"/>, <see cref="Pump"/>, <see cref="SetReverb"/>,
    /// <see cref="Dispose"/>. One background thread renders the queue front to back.
    /// </summary>
    public sealed class SfxClipCache : IDisposable
    {
        /// <summary>
        /// How long a replaced clip is kept beyond its own length: a play may have been scheduled before the swap to start
        /// later (the heartbeat's third beat 1800 ms on, a step's result 280 ms on), and destroying the clip would cut it.
        /// </summary>
        public const double RetireGraceSeconds = 4;

        private sealed class Entry
        {
            public AudioClip Clip;
            public int Generation;
        }

        private sealed class Rendered
        {
            public SfxVariant Variant;
            public RenderedSound Sound;
            public int Generation;
        }

        private readonly Dictionary<SfxVariant, Entry> entries = new Dictionary<SfxVariant, Entry>();
        private readonly object sync = new object();
        private readonly LinkedList<SfxVariant> queue = new LinkedList<SfxVariant>();
        private readonly Dictionary<SfxVariant, LinkedListNode<SfxVariant>> queued = new Dictionary<SfxVariant, LinkedListNode<SfxVariant>>();
        private readonly ConcurrentQueue<Rendered> done = new ConcurrentQueue<Rendered>();
        private readonly List<(AudioClip Clip, double At)> retired = new List<(AudioClip, double)>();
        private Thread worker;
        private bool stopping, disposed;
        private SfxVariant inFlight;
        private bool inFlightSet;
        private double reverb;
        private int generation;

        /// <param name="sampleRate">The rate the clips are rendered at (Unity: AudioSettings.outputSampleRate).</param>
        /// <param name="reverb">The send into the damp room baked into every clip (the game keeps 0.18).</param>
        public SfxClipCache(int sampleRate, double reverb = 0.18)
        {
            SampleRate = sampleRate;
            this.reverb = reverb;
        }

        public int SampleRate { get; }
        public double Reverb => reverb;
        /// <summary>Variants with a clip (or a failed render).</summary>
        public int Count => entries.Count;
        /// <summary>Variants waiting for the background thread.</summary>
        public int Pending
        {
            get { lock (sync) return queue.Count; }
        }
        /// <summary>Variants that had to be rendered on the main thread because a play came before the background render.</summary>
        public int RenderedOnTheSpot { get; private set; }

        /// <summary>True when the variant has a clip at the current reverb.</summary>
        public bool Has(SfxVariant v) => entries.TryGetValue(v, out Entry e) && e.Generation == generation;

        /// <summary>
        /// The clip of <paramref name="v"/>: the cached one (an older reverb's included, until its replacement is ready), else
        /// rendered now. Null when the sound could not be built (the JS's thrown voice).
        /// </summary>
        public AudioClip Get(SfxVariant v)
        {
            if (disposed) return null;
            if (entries.TryGetValue(v, out Entry e)) return e.Clip;
            lock (sync)
            {
                if (queued.TryGetValue(v, out LinkedListNode<SfxVariant> node))
                {
                    queue.Remove(node);
                    queued.Remove(v);
                }
                // The background thread is rendering this very variant: waiting for it costs less than starting over.
                while (inFlightSet && inFlight.Equals(v) && !stopping) Monitor.Wait(sync, 50);
            }
            // Finished renders waiting for the next frame (this one among them, perhaps).
            Pump(int.MaxValue);
            if (entries.TryGetValue(v, out e)) return e.Clip;
            RenderedOnTheSpot++;
            return Store(v, RenderNow(v, reverb), generation).Clip;
        }

        private RenderedSound RenderNow(SfxVariant v, double withReverb)
        {
            try { return PresetRenderer.Render(v.ToRequest(withReverb), SampleRate); }
            catch (Exception) { return null; }
        }

        private Entry Store(SfxVariant v, RenderedSound s, int gen)
        {
            AudioClip clip = null;
            if (s != null && s.Ok && s.Frames > 0)
            {
                clip = AudioClip.Create(v.ToString(), s.Frames, 2, s.SampleRate, false);
                clip.SetData(s.Interleaved, 0);
            }
            if (entries.TryGetValue(v, out Entry old) && old.Clip) retired.Add((old.Clip, Time.realtimeSinceStartupAsDouble + RetireGraceSeconds + old.Clip.length));
            var e = new Entry { Clip = clip, Generation = gen };
            entries[v] = e;
            return e;
        }

        /// <summary>Queues a background render of <paramref name="v"/> (at the front when <paramref name="top"/>); nothing if it is current or queued.</summary>
        public void Enqueue(SfxVariant v, bool top = false)
        {
            if (disposed || Has(v)) return;
            lock (sync)
            {
                if (stopping) return;
                if (queued.TryGetValue(v, out LinkedListNode<SfxVariant> node))
                {
                    if (top && node != queue.First)
                    {
                        queue.Remove(node);
                        queue.AddFirst(node);
                    }
                    return;
                }
                queued[v] = top ? queue.AddFirst(v) : queue.AddLast(v);
                Monitor.Pulse(sync);
            }
            EnsureWorker();
        }

        /// <summary>Queues every variant of <paramref name="list"/> in its order (all of them ahead of the queue when <paramref name="top"/>).</summary>
        public void Enqueue(IReadOnlyList<SfxVariant> list, bool top = false)
        {
            if (top) for (int i = list.Count - 1; i >= 0; i--) Enqueue(list[i], true);
            else for (int i = 0; i < list.Count; i++) Enqueue(list[i]);
        }

        /// <summary>
        /// Changes the room send baked into the clips: every clip is rendered again in the background; until its new render
        /// is ready, the old one plays (js: setReverb ramps the live wet bus; here the room is part of the clip).
        /// </summary>
        public void SetReverb(double r)
        {
            if (disposed || r == reverb) return;
            lock (sync)
            {
                reverb = r;
                generation++;
            }
            var keys = new List<SfxVariant>(entries.Keys);
            foreach (SfxVariant v in keys) Enqueue(v);
        }

        /// <summary>
        /// Main thread, once a frame: turns up to <paramref name="maxClips"/> finished renders into clips and destroys the
        /// replaced clips whose last play has ended.
        /// </summary>
        public void Pump(int maxClips = 4)
        {
            if (disposed) return;
            int made = 0;
            while (made < maxClips && done.TryDequeue(out Rendered r))
            {
                if (r.Generation != generation) continue; // an older reverb: its newer render is queued
                if (entries.TryGetValue(r.Variant, out Entry e) && e.Generation == generation) continue; // rendered on the spot meanwhile
                Store(r.Variant, r.Sound, r.Generation);
                made++;
            }
            if (retired.Count > 0)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                for (int i = retired.Count - 1; i >= 0; i--)
                {
                    if (retired[i].At > now) continue;
                    DestroyClip(retired[i].Clip);
                    retired.RemoveAt(i);
                }
            }
        }

        private void EnsureWorker()
        {
            if (worker != null) return;
            worker = new Thread(Work) { IsBackground = true, Name = "Trial.Audio sfx renders", Priority = System.Threading.ThreadPriority.BelowNormal };
            worker.Start();
        }

        private void Work()
        {
            while (true)
            {
                SfxVariant v;
                double r;
                int gen;
                lock (sync)
                {
                    while (queue.Count == 0 && !stopping) Monitor.Wait(sync);
                    if (stopping) return;
                    v = queue.First.Value;
                    queue.RemoveFirst();
                    queued.Remove(v);
                    r = reverb;
                    gen = generation;
                    inFlight = v;
                    inFlightSet = true;
                }
                RenderedSound s = RenderNow(v, r);
                done.Enqueue(new Rendered { Variant = v, Sound = s, Generation = gen });
                lock (sync)
                {
                    inFlightSet = false;
                    Monitor.PulseAll(sync);
                }
            }
        }

        /// <summary>Stops the background thread and destroys every clip.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lock (sync)
            {
                stopping = true;
                queue.Clear();
                queued.Clear();
                Monitor.PulseAll(sync);
            }
            worker?.Join(2000);
            worker = null;
            foreach (Entry e in entries.Values) DestroyClip(e.Clip);
            entries.Clear();
            foreach (var (clip, _) in retired) DestroyClip(clip);
            retired.Clear();
            while (done.TryDequeue(out Rendered _)) { }
        }

        private static void DestroyClip(AudioClip clip)
        {
            if (!clip) return;
            if (Application.isPlaying) Object.Destroy(clip);
            else Object.DestroyImmediate(clip);
        }

        // ---- what to render first ---------------------------------------------------------------------------------

        private static string TimbreOf(string name) => SoundData.DefaultPreset(name)?.Timbre;

        /// <summary>Every alternation class of a name (three for an alternating timbre, one otherwise).</summary>
        private static void AddClasses(List<SfxVariant> list, string name)
        {
            string timbre = TimbreOf(name);
            bool battle = SoundData.IsBattleName(name);
            int classes = SoundRules.Wobbles(timbre, !battle) ? 3 : 1;
            for (int k = 0; k < classes; k++)
            {
                int turn = classes == 3 ? k : 1;
                list.Add(battle ? SfxVariant.OfBattle(name, timbre, turn) : SfxVariant.OfMeta(name, timbre, turn));
            }
        }

        /// <summary>The 12 footsteps of a ground: nodeStep and nodeReturn × the three alternation classes × right / left.</summary>
        public static List<SfxVariant> StepSet(string surface)
        {
            var list = new List<SfxVariant>(12);
            foreach (string name in new[] { "nodeStep", "nodeReturn" })
            {
                string timbre = TimbreOf(name);
                for (int stepTurn = 1; stepTurn <= 2; stepTurn++)
                    for (int turn = 0; turn < 3; turn++)
                        list.Add(SfxVariant.OfMeta(name, timbre, turn, pan: SoundRules.StepPan(stepTurn), surface: surface));
            }
            return list;
        }

        /// <summary>The four miasma-line swells (20 / 40 / 60 / 80 %), as miasmaLine() asks for them.</summary>
        public static List<SfxVariant> MiasmaLineSet()
        {
            var list = new List<SfxVariant>(4);
            string timbre = TimbreOf("miasmaLine");
            for (int i = 1; i <= 4; i++)
            {
                SoundCall c = SoundRules.MiasmaLineCall(20 * i);
                list.Add(SfxVariant.OfMeta("miasmaLine", timbre, 1, c.Mult, pitchMul: c.PitchMul));
            }
            return list;
        }

        /// <summary>
        /// Everything the game plays, in the order it is first needed: the buttons and the title, the hall, the footsteps of
        /// <paramref name="surface"/>, the map, the rewards, the battle, then the long life and layer sounds.
        /// </summary>
        public static List<SfxVariant> BootSet(string surface)
        {
            var list = new List<SfxVariant>(100);
            foreach (string n in new[] { "uiClick", "uiConfirm", "uiCancel", "nodeHover", "titleEnter" }) AddClasses(list, n);
            foreach (string n in new[] { "hallArrive", "heirNamed" }) AddClasses(list, n);
            list.AddRange(StepSet(surface ?? "stone"));
            foreach (string n in new[] { "journalOpen", "journalClose", "rest", "train", "infoGained", "eventResolve", "traceTake", "consumableUse" }) AddClasses(list, n);
            list.AddRange(MiasmaLineSet());
            foreach (string n in new[] { "rewardReveal", "rewardPick", "cardAdded" }) AddClasses(list, n);
            foreach (string n in SoundData.DsSoundNames) AddClasses(list, n);
            foreach (string n in new[] { "miasmaDeath", "layerCleared", "interlude", "descend", "lifeEndDeath", "survived" }) AddClasses(list, n);
            return list;
        }
    }
}
