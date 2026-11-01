// The live ambience of the trial: the ambience half of createAudio() (scenes/audio/Audio.js:281-306 ensure(), 579-693
// layers and the scheduler, 698 ramp, 739-751 setAmbience, 774-782 unlock, 811-814 duckAmbience, 700-709 the suspend of
// applyMute, 835-841 destroy), rendered block by block on the audio thread (Unity's OnAudioFilterRead, AmbienceHost).
//
// Two halves, as the browser has them:
//  - the caller's half (the JS's synchronous part: ids, the fade default, the surface, the duck value, the volume) runs
//    on the thread that owns the audio object (Unity's main thread) and posts the graph work as commands;
//  - the render half (layers, the scheduler, the timers, the graph) runs where Render is called. Render runs the posted
//    commands first, then the JS timers on the engine's own time: the scheduler every 250 ms (setInterval(tick, TICK_MS))
//    and the kill of a stopped layer fadeMs + 150 ms after its stop (setTimeout(killLayer, …)). Engine time stands still
//    while the engine is frozen (ctx.suspend()), so a muted ambience does not burst when it comes back.
// Nothing here touches Unity; driven offline with the same calls it renders the same samples.
//
// The graph:
//   layer L: beds, drones, voices → L.mix → L.gain ─→ amb (the dry entry) → align ─→ ambVolume → ambDuck → out
//                                             └→ sendRoom (def.room) → ambWet → LP 1800 → cave ─┘
// [deviation] In the JS, amb is one gain (the ambience volume) that sums the dry layers and the cave. Here the volume
// gain (ambVolume) follows the sum, which is the same product, and the dry path is delayed by the cave's block latency
// (`align`, 4096 frames) so that wet and dry stay aligned while the convolution runs in large, cheap blocks: the whole
// ambience lags by 85 ms at 48 kHz, which nothing depends on. The layers still connect to the node named "amb", as in
// the JS call log. The master gain and the compressor that follow ambDuck in the JS are MasterBus's (on the listener).
// [deviation] The JS shares one noise-burst counter (noiseTurn) between the meta plays and the ambience voices, so a
// footstep moves the read position of the next ambience tick. Here the ambience counts its own bursts (the one-shots
// are pre-rendered clips): the ticks read other stretches of the same noise, which nothing can hear.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Trial.Audio.Synth
{
    /// <summary>Delays its (stereo) input by a whole number of frames: the dry ambience waits for the cave's block latency.</summary>
    public sealed class AlignDelayNode : AudioNode
    {
        private readonly int delay;
        private readonly float[] ringL, ringR;
        private int pos;
        private readonly float[][] input = { new float[AudioContextLite.RenderQuantumFrames], new float[AudioContextLite.RenderQuantumFrames] };

        public AlignDelayNode(AudioContextLite context, int frames) : base(context, "Delay")
        {
            if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames));
            delay = frames;
            ringL = new float[Math.Max(1, frames)];
            ringR = new float[Math.Max(1, frames)];
        }

        public int DelayFrames => delay;

        protected override long TailFrames => delay;

        protected override void Process(long startFrame, long quantum)
        {
            int n = AudioContextLite.RenderQuantumFrames;
            int ch = MixInputs(input);
            if (ch == 1) Array.Copy(input[0], input[1], n);
            OutChannels = 2;
            float[] oL = Out[0], oR = Out[1], iL = input[0], iR = input[1];
            if (delay == 0)
            {
                Array.Copy(iL, oL, n);
                Array.Copy(iR, oR, n);
                return;
            }
            for (int k = 0; k < n; k++)
            {
                oL[k] = ringL[pos]; oR[k] = ringR[pos];
                ringL[pos] = iL[k]; ringR[pos] = iR[k];
                if (++pos == delay) pos = 0;
            }
        }
    }

    public sealed class AmbienceEngine
    {
        /// <summary>The cave's convolution block (its latency, and the dry path's delay).</summary>
        public const int CaveBlockFrames = 4096;
        private const double TickS = SoundData.TickMs / 1000;

        public AudioContextLite Ctx { get; }
        /// <summary>The meta synth whose primitives the ambience voices use (its noise counter is the ambience's own).</summary>
        public MetaSynth Synth { get; }
        public int SampleRate => Ctx.SampleRate;

        private readonly GainNode amb, ambVolume, ambDuck, ambWet;
        private readonly BiquadFilterNode ambWetLp;
        private readonly ConvolverNode cave;
        private readonly AlignDelayNode align;

        // ---- the caller's half -----------------------------------------------------------------------------------
        private string ambId;
        private string surface = "stone";
        private double duck = 1, volume;
        private bool unlocked, destroyed, frozen;

        // ---- the render half -------------------------------------------------------------------------------------
        private readonly ConcurrentQueue<Action> commands = new ConcurrentQueue<Action>();
        private readonly List<AmbienceLayer> layers = new List<AmbienceLayer>();
        private string renderAmbId;
        private bool renderUnlocked, renderDestroyed, renderFrozen;
        private double nextTickAt = TickS;
        private long playedFrames;
        private int errors;
        private readonly float[] bufL = new float[CaveBlockFrames], bufR = new float[CaveBlockFrames];

        /// <param name="sampleRate">The output rate (Unity: AudioSettings.outputSampleRate).</param>
        /// <param name="volume">The ambience volume (settings.ambience).</param>
        /// <param name="duck">The duck factor already in force (js: <c>ambDuck = G(duck)</c>).</param>
        /// <param name="log">Records every graph call (tests).</param>
        /// <param name="caveBlockFrames">The cave's convolution block (a power of two ≥ 128; 128 = no latency, much more work).</param>
        // js: scenes/audio/Audio.js:281 (the ambience half of ensure())
        public AmbienceEngine(int sampleRate = 48000, double volume = 0.6, double duck = 1, ICallLog log = null, int caveBlockFrames = CaveBlockFrames)
        {
            Ctx = new AudioContextLite(sampleRate, log);
            this.volume = volume;
            this.duck = duck;
            var bufs = new NoiseBank(sampleRate);
            // No effects bus: every ambience voice routes to its layer's mix (o.dst).
            Synth = new MetaSynth(Ctx, null, null, bufs);
            ambDuck = Synth.G(duck); ambDuck.LogName = "ambDuck"; ambDuck.Connect(Ctx.Destination);
            ambVolume = Synth.G(volume); ambVolume.LogName = "ambVolume"; ambVolume.Connect(ambDuck);
            cave = Ctx.CreateConvolver(caveBlockFrames); cave.LogName = "cave";
            amb = Synth.G(1); amb.LogName = "amb";
            align = new AlignDelayNode(Ctx, cave.LatencyFrames) { LogName = "ambAlign" };
            amb.Connect(align); align.Connect(ambVolume);
            // The ambience gets a longer, darker cave.
            ambWet = Synth.G(1); ambWet.LogName = "ambWet";
            ambWetLp = Synth.F(BiquadType.Lowpass, 1800); ambWetLp.LogName = "ambWetLp";
            ambWet.Connect(ambWetLp);
            cave.Buffer = AudioBuffers.MakeCave(sampleRate);
            ambWetLp.Connect(cave); cave.Connect(ambVolume);
            // Everything made from here on (layers, voices) leaves the graph once it can no longer sound.
            Ctx.TransientNodes = true;
            // Build the band-limited tables the voices read now, not on the audio thread at a voice's first use.
            WaveTables.For(OscType.Triangle, sampleRate);
            WaveTables.For(OscType.Sawtooth, sampleRate);
        }

        // ---- the caller's half (the JS's synchronous part) ----------------------------------------------------------

        /// <summary>js: <c>audio.ambience</c>: the place asked for last (null = silence).</summary>
        public string Ambience => ambId;
        /// <summary>js: <c>audio.surface</c>: the footstep ground of the last place asked for (stone before any).</summary>
        public string Surface => surface;
        /// <summary>The duck factor in force (1 = none).</summary>
        public double Duck => duck;
        /// <summary>The ambience volume in force.</summary>
        public double Volume => volume;
        public bool Unlocked => unlocked;
        public bool Destroyed => destroyed;
        /// <summary>True while the engine is frozen (js: the meta context suspended 400 ms after a mute).</summary>
        public bool Frozen => frozen;

        private void Post(Action a) => commands.Enqueue(a);

        /// <summary>
        /// js: scenes/audio/Audio.js:739 setAmbience(id, fadeMs = 1800): crossfades to a place (<c>"title"</c>, <c>"hall"</c>,
        /// <c>"interlude"</c>, <c>"layer1"</c>..<c>"layer7"</c>, or 1..7); null, "" or "none" fade to silence. False for an unknown
        /// id. The surface follows at once; the same place with a live layer does not restart; before <see cref="Unlock"/> it only
        /// records the place. As in the JS, a fractional layer number (3.5 → "layer3.5") is not a place and throws.
        /// </summary>
        public bool SetAmbience(object id, double? fadeMs = null)
        {
            if (!SoundRules.TryAmbienceId(id, out string nid)) return false;
            double fade = fadeMs == null ? SoundData.DefaultAmbienceFadeMs : Math.Max(0, fadeMs.Value);
            if (nid != null)
            {
                // js: AMBIENCE_DEFS[nid].surface (a TypeError for an id ambienceId lets through but no place has)
                if (!SoundData.AmbienceDefs.TryGetValue(nid, out AmbienceDef def)) throw new KeyNotFoundException("no ambience " + nid);
                surface = def.Surface;
            }
            ambId = nid;
            Post(() => RenderSetAmbience(nid, fade));
            return true;
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:811 duckAmbience(factor, fadeMs = 600): lowers the ambience (not persisted). Before
        /// <see cref="Unlock"/> the JS has no graph yet and only keeps the value, which <c>ensure()</c> then builds in
        /// (<c>ambDuck = G(duck)</c>): here it is set at once, without a glide.
        /// </summary>
        public void DuckAmbience(double factor, double? fadeMs = null)
        {
            duck = SoundRules.Clamp01(factor, 1);
            double d = duck, tc = Math.Max(0.01, (fadeMs ?? SoundData.DefaultDuckFadeMs) / 3000);
            Post(() =>
            {
                if (renderDestroyed) return;
                double now = Now;
                ambDuck.Gain.CancelScheduledValues(now);
                if (renderUnlocked) ambDuck.Gain.SetTargetAtTime(d, now, tc);
                else ambDuck.Gain.SetValueAtTime(d, now);
            });
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:817 the ambience volume's ramp (<c>ramp(amb.gain, v)</c>: cancel(now), setTarget(v, now, 0.03)).
        /// Before <see cref="Unlock"/> it is set at once (js: <c>if (amb) ramp(…)</c>, then <c>amb = G(state.ambience)</c> in ensure()).
        /// </summary>
        public void SetVolume(double v)
        {
            volume = v;
            Post(() =>
            {
                if (renderDestroyed) return;
                if (renderUnlocked) Ramp(ambVolume.Gain, v);
                else
                {
                    double now = Now;
                    ambVolume.Gain.CancelScheduledValues(now);
                    ambVolume.Gain.SetValueAtTime(v, now);
                }
            });
        }

        /// <summary>js: scenes/audio/Audio.js:774 unlock() (its ambience part): the pending place starts with a 2500 ms fade.</summary>
        public bool Unlock()
        {
            if (destroyed) return false;
            unlocked = true;
            Post(RenderUnlock);
            return true;
        }

        /// <summary>
        /// js: <c>ctx.suspend()</c> / <c>ctx.resume()</c> (applyMute, Audio.js:700-709): a frozen engine renders silence and its time
        /// stands still (its scheduled voices and fades wait).
        /// </summary>
        public void SetFrozen(bool on)
        {
            frozen = on;
            Post(() => renderFrozen = on);
        }

        /// <summary>js: scenes/audio/Audio.js:835 destroy() (its ambience part): every layer is killed and the engine renders silence.</summary>
        public void Destroy()
        {
            destroyed = true;
            Post(RenderDestroy);
        }

        /// <summary>
        /// A new engine at another sample rate that carries this one's state (the volume, the duck, the place, the surface,
        /// unlocked, frozen); the place starts again with a short fade. This engine is destroyed. For a device change
        /// (Unity's AudioSettings.OnAudioConfigurationChanged).
        /// </summary>
        public AmbienceEngine CloneAt(int sampleRate, double fadeMs = 300)
        {
            var e = new AmbienceEngine(sampleRate, volume, duck);
            e.surface = surface;
            if (destroyed) e.Destroy();
            else
            {
                if (unlocked) e.Unlock();
                if (ambId != null) e.SetAmbience(ambId, fadeMs);
                if (frozen) e.SetFrozen(true);
            }
            Destroy();
            return e;
        }

        // ---- the render half ---------------------------------------------------------------------------------------

        /// <summary>Tests: the time the engine's calls read as "now" (the JS mock's settable currentTime) in place of the context's and the played time.</summary>
        public double? NowOverride { get; set; }
        /// <summary>js: <c>ctx.currentTime</c>.</summary>
        public double Now => NowOverride ?? Ctx.CurrentTime;
        /// <summary>The time of the frames handed out so far (the timers run on it).</summary>
        public double PlayedTime => (double)playedFrames / SampleRate;
        private double TimerNow => NowOverride ?? PlayedTime;

        /// <summary>The layers, the fading ones included (render half: read it on the render thread, or offline).</summary>
        public IReadOnlyList<AmbienceLayer> Layers => layers;
        /// <summary>The layer that is not fading out, or null.</summary>
        public AmbienceLayer LiveLayer
        {
            get
            {
                for (int i = 0; i < layers.Count; i++) if (!layers[i].Stopping) return layers[i];
                return null;
            }
        }
        /// <summary>js: <c>errors</c>: voices and commands that threw (skipped, not fatal).</summary>
        public int Errors => errors;

        /// <summary>
        /// Runs the posted commands now, in order. Render does this at the start of every block; call it yourself only when
        /// nothing renders at the same time (tests, offline renders).
        /// </summary>
        public void Flush()
        {
            while (commands.TryDequeue(out Action a))
            {
                try { a(); }
                catch (Exception) { errors++; }
            }
        }

        // js: scenes/audio/Audio.js:698
        private void Ramp(AudioParam param, double v)
        {
            double now = Now;
            param.CancelScheduledValues(now);
            param.SetTargetAtTime(v, now, 0.03);
        }

        // js: scenes/audio/Audio.js:744-750 (the graph half of setAmbience)
        private void RenderSetAmbience(string nid, double fade)
        {
            if (renderDestroyed) return;
            AmbienceLayer live = LiveLayer;
            if (nid == renderAmbId && (live != null || !renderUnlocked)) return;
            renderAmbId = nid;
            if (!renderUnlocked) return; // starts on unlock()
            for (int i = 0, n = layers.Count; i < n; i++) StopLayer(layers[i], fade);
            if (nid != null) StartLayer(nid, fade);
        }

        // js: scenes/audio/Audio.js:776-780 (the ambience half of unlock)
        private void RenderUnlock()
        {
            if (renderDestroyed) return;
            renderUnlocked = true;
            if (renderAmbId != null && LiveLayer == null) StartLayer(renderAmbId, SoundData.UnlockFadeMs);
        }

        // js: scenes/audio/Audio.js:838
        private void RenderDestroy()
        {
            if (renderDestroyed) return;
            foreach (AmbienceLayer L in layers.ToArray()) KillLayer(L);
            renderDestroyed = true;
        }

        // js: scenes/audio/Audio.js:579
        private AmbienceLayer MakeLayer(string id)
        {
            AmbienceDef def = SoundData.AmbienceDefs[id];
            double now = Now;
            var L = new AmbienceLayer(id, def);
            L.Gain = Synth.G(0.0001); L.Gain.Connect(amb);
            L.Mix = Synth.G(1); L.Mix.Connect(L.Gain);
            L.SendRoom = Synth.G(def.Room); L.Gain.Connect(L.SendRoom); L.SendRoom.Connect(ambWet);
            AmbienceBuilders.Build(id, new AmbienceKit(Synth, L, now), L.R);
            return L;
        }

        // js: scenes/audio/Audio.js:655
        private AmbienceLayer StartLayer(string id, double fadeMs)
        {
            AmbienceLayer L = MakeLayer(id);
            double now = Now, f = Math.Max(0.05, fadeMs / 1000);
            L.Gain.Gain.SetValueAtTime(0.0001, now);
            L.Gain.Gain.LinearRampToValueAtTime(L.Def.Level, now + f);
            layers.Add(L);
            Tick();
            return L;
        }

        // js: scenes/audio/Audio.js:663
        private void KillLayer(AmbienceLayer L)
        {
            L.KillAt = double.PositiveInfinity;
            foreach (ScheduledSourceNode n in L.Nodes)
            {
                try { n.Stop(); }
                catch (Exception) { /* already stopped */ }
            }
            try { L.Gain.Disconnect(); }
            catch (Exception) { /* ignore */ }
            layers.Remove(L);
        }

        // js: scenes/audio/Audio.js:670
        private void StopLayer(AmbienceLayer L, double fadeMs)
        {
            if (L.Stopping) return;
            L.Stopping = true;
            double now = Now, f = Math.Max(0.05, fadeMs / 1000);
            AudioParam p = L.Gain.Gain;
            double cur = !double.IsNaN(p.Value) && !double.IsInfinity(p.Value) ? p.Value : L.Def.Level;
            p.CancelScheduledValues(now); p.SetValueAtTime(cur, now); p.LinearRampToValueAtTime(0, now + f);
            L.KillAt = TimerNow + (fadeMs + 150) / 1000;
        }

        /// <summary>js: scenes/audio/Audio.js:679 tick() (<c>audio._tick</c>): schedules the sparse ambience events that fall inside the lookahead window.</summary>
        public void Tick()
        {
            if (renderDestroyed) return;
            double now = Now, horizon = now + SoundData.LookaheadS;
            for (int li = 0; li < layers.Count; li++)
            {
                AmbienceLayer L = layers[li];
                if (L.Stopping) continue;
                foreach (SparseVoice s in L.Sparse)
                {
                    if (s.Next < now - 1) s.Next = now + 0.05; // after a long suspend, do not burst
                    while (s.Next < horizon)
                    {
                        try { s.Fn(Math.Max(s.Next, now + 0.02), s.K, L.R); }
                        catch (Exception) { errors++; /* skip a bad voice */ }
                        s.K++;
                        s.Next += s.Lo + (s.Hi - s.Lo) * L.Rnd.Next();
                    }
                }
            }
        }

        /// <summary>
        /// The JS timers, on engine time: kills the stopped layers whose time has come (<c>setTimeout(killLayer, fadeMs + 150)</c>)
        /// and runs the scheduler when its 250 ms interval is due (<c>setInterval(tick, TICK_MS)</c>).
        /// </summary>
        public void RunTimers()
        {
            if (renderDestroyed) return;
            double t = TimerNow;
            for (int i = layers.Count - 1; i >= 0; i--)
                if (i < layers.Count && layers[i].KillAt <= t) KillLayer(layers[i]);
            if (t >= nextTickAt)
            {
                while (nextTickAt <= t) nextTickAt += TickS;
                Tick();
            }
        }

        private double NextTimerTime()
        {
            double next = nextTickAt;
            for (int i = 0; i < layers.Count; i++) if (layers[i].KillAt < next) next = layers[i].KillAt;
            return next;
        }

        /// <summary>
        /// Renders <paramref name="frames"/> frames into an interleaved buffer of <paramref name="channels"/> (1: the mono mix
        /// (L+R)/2; 2: L, R; more: L, R and silence). Runs the posted commands first; frozen or destroyed, it writes silence and
        /// time stands still. The block is cut where a timer falls, so the timers fire at the same frames whatever the
        /// caller's block size.
        /// </summary>
        public void Render(float[] data, int frames, int channels)
        {
            Flush();
            if (renderDestroyed || renderFrozen)
            {
                Array.Clear(data, 0, Math.Min(data.Length, frames * channels));
                return;
            }
            int done = 0;
            while (done < frames)
            {
                RunTimers();
                int n = Math.Min(frames - done, bufL.Length);
                double ahead = Math.Ceiling((NextTimerTime() - PlayedTime) * SampleRate - 1e-9);
                if (ahead >= 1 && ahead < n) n = (int)ahead;
                Ctx.Render(bufL, bufR, 0, n);
                Interleave(data, done, n, channels);
                playedFrames += n;
                done += n;
            }
        }

        private void Interleave(float[] data, int offset, int n, int channels)
        {
            if (channels == 1)
            {
                for (int i = 0; i < n; i++) data[offset + i] = 0.5f * (bufL[i] + bufR[i]);
            }
            else if (channels == 2)
            {
                for (int i = 0; i < n; i++)
                {
                    int o = 2 * (offset + i);
                    data[o] = bufL[i];
                    data[o + 1] = bufR[i];
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    int o = (offset + i) * channels;
                    data[o] = bufL[i];
                    data[o + 1] = bufR[i];
                    for (int c = 2; c < channels; c++) data[o + c] = 0;
                }
            }
        }
    }
}
