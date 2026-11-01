// js: scenes/audio/Audio.js:579-652 (an ambience layer and the toolkit K its builder plays with) and 852-964 (the ten
// ambiences, AMBIENCE_BUILDERS), line by line in the same node-creation and param-call order, so the call log of a place
// equals the JS's (unity-port/tools/audio-golden.mjs, "ambience").
// The ambiences (world-v1.md §4). Gains are tiny on purpose: the ambience sits under everything.
// Every builder draws its random numbers from the layer's own seeded generator (L.rnd), in source order: arguments are
// evaluated left to right in C# as in JS, so `i * (0.07 + 0.05 * r())` calls r() even when i is 0, as the JS does.
using System;
using System.Collections.Generic;

namespace Trial.Audio.Synth
{
    /// <summary>js: one sparse voice of a layer, <c>{ lo, hi, fn, k, next }</c> (Audio.js:612).</summary>
    public sealed class SparseVoice
    {
        /// <summary>The shortest and longest gap between two firings, seconds.</summary>
        public double Lo, Hi;
        /// <summary><c>fn(t, k, rnd)</c>: builds the voice at time t; k counts the firings from 0.</summary>
        public Action<double, int, Func<double>> Fn;
        /// <summary>js: <c>k</c>, how many times the voice fired.</summary>
        public int K;
        /// <summary>js: <c>next</c>, the context time of the next firing.</summary>
        public double Next;
    }

    /// <summary>
    /// js: an ambience layer <c>L = { id, def, nodes, sparse, rnd, stopping, timer, gain, mix }</c> (Audio.js:581).
    /// <see cref="Nodes"/> are the sources that run until the layer is killed (beds, drones and their LFOs).
    /// </summary>
    public sealed class AmbienceLayer
    {
        public readonly string Id;
        public readonly AmbienceDef Def;
        public readonly List<ScheduledSourceNode> Nodes = new List<ScheduledSourceNode>();
        public readonly List<SparseVoice> Sparse = new List<SparseVoice>();
        /// <summary>js: <c>L.rnd = lcg(hashSeed(id))</c>.</summary>
        public readonly Lcg Rnd;
        /// <summary><see cref="Rnd"/> as the <c>r</c> a builder's voices receive (one delegate per layer).</summary>
        public readonly Func<double> R;
        /// <summary>js: <c>L.stopping</c>: the layer is fading out and schedules nothing new.</summary>
        public bool Stopping;
        /// <summary>js: <c>L.gain</c> (the fade, 0.0001 → def.level) and <c>L.mix</c> (every bed, drone and voice sums here).</summary>
        public GainNode Gain, Mix;
        /// <summary>js: <c>sendRoom</c>, the post-fade send into the cave (gain def.room).</summary>
        public GainNode SendRoom;
        /// <summary>The engine time at which the stopped layer is killed (js: the <c>fadeMs + 150</c> timeout); ∞ while it plays.</summary>
        public double KillAt = double.PositiveInfinity;

        public AmbienceLayer(string id, AmbienceDef def)
        {
            Id = id;
            Def = def;
            Rnd = new Lcg(ParkMiller.HashSeed(id));
            R = Rnd.Next;
        }
    }

    /// <summary>js: the options of <c>K.bed(color, { type = 'lowpass', f, q = 0.7, gain, pan = 0, fLfo, gLfo })</c>.</summary>
    public sealed class BedOptions
    {
        public BiquadType Type = BiquadType.Lowpass;
        public double F;
        public double Q = 0.7;
        public double Gain;
        public double Pan;
        /// <summary><c>[rate Hz, depth Hz]</c> sine LFOs added to the filter's frequency.</summary>
        public (double Rate, double Depth)[] FLfo;
        /// <summary><c>[rate Hz, depth]</c> sine LFOs added to the bed's gain.</summary>
        public (double Rate, double Depth)[] GLfo;
    }

    /// <summary>
    /// js: scenes/audio/Audio.js:594 K, the toolkit an ambience builds with. Beds and drones run until the layer stops;
    /// voices are one-shots. Everything goes into the layer's mix (<c>o = { dst: L.mix }</c>: no effects bus, no pan).
    /// </summary>
    public sealed class AmbienceKit
    {
        private readonly AudioContextLite ctx;
        private readonly MetaSynth syn;
        private readonly AmbienceLayer layer;
        private readonly double now;
        private readonly VoiceRouting o;

        /// <param name="synth">The meta synth whose primitives (G, F, O, N, env, swellEnv, thud) the voices use; its noise
        /// counter is the one every burst shares.</param>
        /// <param name="now">js: <c>ctx.currentTime</c> when the layer was made (beds, drones and LFOs start then).</param>
        public AmbienceKit(MetaSynth synth, AmbienceLayer layer, double now)
        {
            syn = synth;
            ctx = synth.Ctx;
            this.layer = layer;
            this.now = now;
            o = new VoiceRouting { Dst = layer.Mix };
        }

        public AmbienceLayer Layer => layer;

        private T Keep<T>(T n) where T : ScheduledSourceNode
        {
            layer.Nodes.Add(n);
            return n;
        }

        // js: scenes/audio/Audio.js:586
        private void Lfo(AudioParam param, (double Rate, double Depth)[] list)
        {
            if (list == null) return;
            foreach ((double rate, double depth) in list)
            {
                var x = Keep(ctx.CreateOscillator()); x.Type = OscType.Sine; x.Frequency.Value = rate;
                var dg = syn.G(depth); x.Connect(dg); dg.Connect(param); x.Start(now);
            }
        }

        // js: scenes/audio/Audio.js:595
        public void Bed(NoiseColor color, BedOptions b)
        {
            var s = Keep(ctx.CreateBufferSource()); s.Buffer = syn.Bufs.Get(color); s.Loop = true;
            var fl = syn.F(b.Type, b.F, b.Q); var g = syn.G(b.Gain);
            s.Connect(fl); fl.Connect(g);
            AudioNode last = g;
            if (b.Pan != 0 && !double.IsNaN(b.Pan)) { var p = ctx.CreateStereoPanner(); p.Pan.Value = b.Pan; g.Connect(p); last = p; }
            last.Connect(layer.Mix);
            Lfo(fl.Frequency, b.FLfo); Lfo(g.Gain, b.GLfo);
            s.Start(now, (layer.Nodes.Count * 0.61) % (AudioBuffers.NoiseSeconds - 0.5));
        }

        // js: scenes/audio/Audio.js:605
        public void Drone(OscType type, double f, double gain, double lp = 0, (double Rate, double Depth)[] gLfo = null)
        {
            var x = Keep(ctx.CreateOscillator()); x.Type = type; x.Frequency.Value = f;
            var g = syn.G(gain);
            if (lp != 0) { var fl = syn.F(BiquadType.Lowpass, lp, 0.7); x.Connect(fl); fl.Connect(g); } else x.Connect(g);
            g.Connect(layer.Mix); Lfo(g.Gain, gLfo); x.Start(now);
        }

        // js: scenes/audio/Audio.js:611
        public void Every(double lo, double hi, Action<double, int, Func<double>> fn)
        {
            layer.Sparse.Add(new SparseVoice { Lo = lo, Hi = hi, Fn = fn, K = 0, Next = now + lo * 0.5 + (hi - lo) * 0.5 * layer.Rnd.Next() });
        }

        // ---- one-shot voices --------------------------------------------------------------------------------------

        // js: scenes/audio/Audio.js:615
        public void Tone(double t, double f, double v, double a, double d, OscType type = OscType.Sine, double lp = 0)
        {
            var x = syn.O(type, f, t, a + d); var g = syn.G(0);
            if (lp != 0) { var fl = syn.F(BiquadType.Lowpass, lp, 0.7); x.Connect(fl); fl.Connect(g); } else x.Connect(g);
            MetaSynth.SwellEnv(g, t, a, v, a + d); g.Connect(layer.Mix);
        }

        // js: scenes/audio/Audio.js:620
        public void Tick(double t, double f, double q, double v, double d)
        {
            var n = syn.N(t, d); var b = syn.F(BiquadType.Bandpass, f, q); var g = syn.G(0);
            n.Connect(b); b.Connect(g); MetaSynth.Env(g, t, 0.001, v, d); g.Connect(layer.Mix);
        }

        // js: scenes/audio/Audio.js:624
        public void Drip(double t, double f, double v)
        {
            var x = syn.O(OscType.Sine, f, t, 0.12); var g = syn.G(0);
            x.Frequency.ExponentialRampToValueAtTime(f * 0.45, t + 0.06); x.Connect(g); MetaSynth.Env(g, t, 0.002, v, 0.09); g.Connect(layer.Mix);
        }

        // js: scenes/audio/Audio.js:628
        public void Thud(double t, double f, double v, double d)
        {
            syn.Thud(t, f, d, v, 0.3, o);
        }

        /// <summary>js: scenes/audio/Audio.js:629 creak: a beam settles: a slowing pulse train through a narrow band.</summary>
        public void Creak(double t, double f, double v)
        {
            var x = syn.O(OscType.Sawtooth, 44, t, 0.45); var b = syn.F(BiquadType.Bandpass, f, 5); var g = syn.G(0);
            x.Frequency.ExponentialRampToValueAtTime(27, t + 0.42);
            x.Connect(b); b.Connect(g); MetaSynth.SwellEnv(g, t, 0.06, v, 0.42); g.Connect(layer.Mix);
        }

        /// <summary>js: scenes/audio/Audio.js:634 scrape: something heavy slides along stone.</summary>
        public void Scrape(double t, double f, double v, double d)
        {
            var n = syn.N(t, d, NoiseColor.Pink); var b = syn.F(BiquadType.Bandpass, f, 1.6); var g = syn.G(0);
            b.Frequency.SetValueAtTime(f * 0.8, t); b.Frequency.LinearRampToValueAtTime(f * 1.15, t + d);
            n.Connect(b); b.Connect(g);
            g.Gain.SetValueAtTime(0.0001, t); g.Gain.LinearRampToValueAtTime(v, t + d * 0.5); g.Gain.LinearRampToValueAtTime(0.0001, t + d);
            g.Connect(layer.Mix);
        }

        /// <summary>js: scenes/audio/Audio.js:641 rumble: distant thunder: dark noise that rolls and darkens.</summary>
        public void Rumble(double t, double v, double f, double a, double d)
        {
            var n = syn.N(t, a + d, NoiseColor.Brown); var b = syn.F(BiquadType.Lowpass, f * 1.6, 0.9); var g = syn.G(0);
            b.Frequency.SetValueAtTime(f * 1.6, t); b.Frequency.ExponentialRampToValueAtTime(f * 0.7, t + a + d);
            n.Connect(b); b.Connect(g); MetaSynth.SwellEnv(g, t, a, v, a + d); g.Connect(layer.Mix);
        }

        /// <summary>js: scenes/audio/Audio.js:646 glide: a slow sliding tone (a far groan, a falling breath).</summary>
        public void Glide(double t, double f0, double f1, double v, double d)
        {
            var x = syn.O(OscType.Sine, f0, t, d); var x2 = syn.O(OscType.Triangle, f0 * 2, t, d); var g2 = syn.G(0.2); var g = syn.G(0);
            x.Frequency.ExponentialRampToValueAtTime(f1, t + d); x2.Frequency.ExponentialRampToValueAtTime(f1 * 2, t + d);
            x.Connect(g); x2.Connect(g2); g2.Connect(g); MetaSynth.SwellEnv(g, t, d * 0.4, v, d); g.Connect(layer.Mix);
        }
    }

    /// <summary>js: scenes/audio/Audio.js:856 AMBIENCE_BUILDERS: one builder per place.</summary>
    public static class AmbienceBuilders
    {
        /// <summary>js: scenes/audio/Audio.js:964 AMBIENCE_BUILDER_IDS (key order kept).</summary>
        public static readonly string[] Ids = { "title", "hall", "interlude", "layer1", "layer2", "layer3", "layer4", "layer5", "layer6", "layer7" };

        public static bool Has(string id) => id != null && Array.IndexOf(Ids, id) >= 0;

        private static (double Rate, double Depth)[] Lfo(params (double Rate, double Depth)[] list) => list;

        /// <summary>js: <c>AMBIENCE_BUILDERS[id](K, L.rnd)</c>; false for a place without a builder.</summary>
        public static bool Build(string id, AmbienceKit K, Func<double> r)
        {
            switch (id)
            {
                case "title": Title(K); return true;
                case "hall": Hall(K); return true;
                case "interlude": Interlude(K); return true;
                case "layer1": Layer1(K); return true;
                case "layer2": Layer2(K); return true;
                case "layer3": Layer3(K); return true;
                case "layer4": Layer4(K); return true;
                case "layer5": Layer5(K); return true;
                case "layer6": Layer6(K); return true;
                case "layer7": Layer7(K); return true;
                default: return false;
            }
        }

        private static readonly double[] TitleTones = { 146.83, 110, 130.81 };

        /// <summary>js: scenes/audio/Audio.js:858 Title — the mouth of the den: wind drawn inward, a low D, now and then a far string.</summary>
        public static void Title(AmbienceKit K)
        {
            K.Bed(NoiseColor.Pink, new BedOptions { Type = BiquadType.Lowpass, F = 380, Q = 0.6, Gain = 0.07, FLfo = Lfo((0.071, 160), (0.023, 90)), GLfo = Lfo((0.031, 0.025)) });
            K.Bed(NoiseColor.Brown, new BedOptions { Type = BiquadType.Lowpass, F = 140, Gain = 0.08 });
            K.Drone(OscType.Sine, 73.42, 0.016, gLfo: Lfo((0.05, 0.006)));
            K.Drone(OscType.Sine, 110.3, 0.006);
            K.Every(14, 22, (t, k, r) => K.Tone(t, TitleTones[k % 3], 0.010, 1.6, 4.5, OscType.Triangle, 600));
        }

        /// <summary>js: scenes/audio/Audio.js:866 継承の間 — a still room with a lamp: no wind, the flame breathing, a warm fifth, a beam settling.</summary>
        public static void Hall(AmbienceKit K)
        {
            K.Bed(NoiseColor.Brown, new BedOptions { Type = BiquadType.Lowpass, F = 240, Gain = 0.06 });
            K.Bed(NoiseColor.Pink, new BedOptions { Type = BiquadType.Bandpass, F = 820, Q = 0.7, Gain = 0.006, GLfo = Lfo((0.83, 0.002), (2.3, 0.0015)) });
            K.Drone(OscType.Sine, 73.42, 0.012);
            K.Drone(OscType.Sine, 110, 0.0065, gLfo: Lfo((0.037, 0.002)));
            K.Drone(OscType.Sine, 146.83, 0.003);
            K.Every(16, 30, (t, k, r) => K.Creak(t, 190, 0.010));
        }

        private static readonly double[] InterludeTones = { 220, 293.66, 246.94, 329.63 };

        /// <summary>js: scenes/audio/Audio.js:875 階層間の休憩 — a pocket of rest: slow breathing air, a low A and E, a soft tone now and then.</summary>
        public static void Interlude(AmbienceKit K)
        {
            K.Bed(NoiseColor.Pink, new BedOptions { Type = BiquadType.Lowpass, F = 520, Gain = 0.05, GLfo = Lfo((0.12, 0.022)) });
            K.Drone(OscType.Sine, 55, 0.014);
            K.Drone(OscType.Sine, 82.41, 0.008, gLfo: Lfo((0.045, 0.003)));
            K.Every(9, 15, (t, k, r) => K.Tone(t, InterludeTones[k % 4], 0.006, 1.1, 3.6));
        }

        private static readonly double[] Layer1Tones = { 659.26, 783.99, 880 };

        /// <summary>
        /// js: scenes/audio/Audio.js:883 1 燦光の樹海 — sunlit sunken forest: open air through the hole in the roof, leaves in slow
        /// gusts, twigs and leaf litter, a few soft motes of light. Almost no drone: the only layer lit by the sky.
        /// </summary>
        public static void Layer1(AmbienceKit K)
        {
            K.Bed(NoiseColor.Pink, new BedOptions { Type = BiquadType.Lowpass, F = 900, Gain = 0.03, FLfo = Lfo((0.043, 320)) });
            K.Bed(NoiseColor.White, new BedOptions { Type = BiquadType.Bandpass, F = 3400, Q = 0.5, Gain = 0.010, GLfo = Lfo((0.052, 0.006), (0.13, 0.003)) });
            K.Drone(OscType.Sine, 110, 0.003);
            K.Every(2.5, 7, (t, k, r) =>
            {
                int n = 1 + k % 3;
                for (int i = 0; i < n; i++) K.Tick(t + i * (0.07 + 0.05 * r()), 2200 + 1400 * r(), 2.5, 0.005, 0.018);
            });
            K.Every(10, 18, (t, k, r) => K.Tone(t, Layer1Tones[k % 3], 0.0035, 0.5, 2.6));
        }

        private static readonly double[] Layer2Tones = { 1567.98, 1396.91 };

        /// <summary>
        /// js: scenes/audio/Audio.js:895 2 ■■の秘跡 — the dragon god's old sanctuary: wind resonating in the pillar rows, drifting
        /// sand, a faint hum of stone, the first violet crystals barely ringing, a far stone settling.
        /// </summary>
        public static void Layer2(AmbienceKit K)
        {
            K.Bed(NoiseColor.Pink, new BedOptions { Type = BiquadType.Bandpass, F = 340, Q = 6, Gain = 0.05, FLfo = Lfo((0.029, 38)), GLfo = Lfo((0.047, 0.018)) });
            K.Bed(NoiseColor.Pink, new BedOptions { Type = BiquadType.Bandpass, F = 512, Q = 8, Gain = 0.025, FLfo = Lfo((0.041, 30)), GLfo = Lfo((0.061, 0.012)) });
            K.Bed(NoiseColor.White, new BedOptions { Type = BiquadType.Bandpass, F = 6500, Q = 0.6, Gain = 0.0018, GLfo = Lfo((0.09, 0.0018)) });
            K.Drone(OscType.Sine, 110, 0.007);
            K.Drone(OscType.Sine, 110.55, 0.007);
            K.Drone(OscType.Sine, 165, 0.003);
            K.Every(14, 26, (t, k, r) => K.Tone(t, Layer2Tones[k % 2], 0.0022, 0.02, 1.8, OscType.Sine, 3000));
            K.Every(20, 34, (t, k, r) => { K.Thud(t, 68, 0.03, 0.5); K.Tick(t + 0.03, 1800, 0.8, 0.004, 0.25); });
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:907 3 大黒蛇の住処 — the dark, damp den: low air swelling with the serpent's breath (about 11 s),
        /// a very low drone, water drops, the coils shifting against the wall.
        /// </summary>
        public static void Layer3(AmbienceKit K)
        {
            K.Bed(NoiseColor.Brown, new BedOptions { Type = BiquadType.Lowpass, F = 190, Gain = 0.10, GLfo = Lfo((0.09, 0.05)) });
            K.Drone(OscType.Sawtooth, 41.2, 0.018, lp: 150);
            K.Drone(OscType.Sine, 82.41, 0.005);
            K.Every(3, 9, (t, k, r) => K.Drip(t, 1100 + 700 * r(), 0.012));
            K.Every(18, 32, (t, k, r) => K.Scrape(t, 380, 0.012, 2.6));
        }

        private static readonly double[] Layer4Haze = { 523.25, 524.1, 784, 785.3, 1046.5 };
        private static readonly double[] Layer4Tones = { 783.99, 880, 1174.66, 1318.51 };

        /// <summary>
        /// js: scenes/audio/Audio.js:916 4 輝晶の間 — the crystal hall: a haze of slowly beating glass tones in the middle register, a
        /// thin hiss of light, crystals that swell and fade. Bright to the eye, still quiet to the ear.
        /// </summary>
        public static void Layer4(AmbienceKit K)
        {
            for (int i = 0; i < Layer4Haze.Length; i++) K.Drone(OscType.Sine, Layer4Haze[i], 0.0016, gLfo: Lfo((0.07 + i * 0.03, 0.0014)));
            K.Bed(NoiseColor.White, new BedOptions { Type = BiquadType.Bandpass, F = 6200, Q = 0.8, Gain = 0.0025 });
            K.Drone(OscType.Sine, 220, 0.004);
            K.Drone(OscType.Sine, 329.63, 0.0025);
            K.Every(7, 13, (t, k, r) => K.Tone(t, Layer4Tones[k % 4], 0.004, 1.5, 4.2));
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:925 5 血染めの死海 — the dried red sea bed: dry gusts, wind whistling through the giant ribs, the
        /// salt crust crackling, something huge groaning far away.
        /// </summary>
        public static void Layer5(AmbienceKit K)
        {
            K.Bed(NoiseColor.Pink, new BedOptions { Type = BiquadType.Lowpass, F = 650, Gain = 0.05, GLfo = Lfo((0.035, 0.022), (0.11, 0.008)) });
            K.Bed(NoiseColor.White, new BedOptions { Type = BiquadType.Bandpass, F = 620, Q = 14, Gain = 0.02, FLfo = Lfo((0.021, 70)), GLfo = Lfo((0.058, 0.012)) });
            K.Drone(OscType.Sine, 49, 0.012);
            K.Drone(OscType.Sine, 73.42, 0.004);
            K.Every(5, 12, (t, k, r) =>
            {
                int n = 3 + k % 4;
                for (int i = 0; i < n; i++) K.Tick(t + i * (0.03 + 0.06 * r()), 2600 + 900 * r(), 2, 0.0035, 0.008);
            });
            K.Every(22, 36, (t, k, r) => K.Glide(t, 65.41, 51.91, 0.010, 3.2));
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:938 6 獄雷峡 — the gorge of black lightning: wind down the gorge and its edge on the cliffs, a
        /// dark low drone, and now and then the lightning crackling along a cliff with thunder rolling far off.
        /// </summary>
        public static void Layer6(AmbienceKit K)
        {
            K.Bed(NoiseColor.Pink, new BedOptions { Type = BiquadType.Bandpass, F = 260, Q = 1.2, Gain = 0.07, GLfo = Lfo((0.05, 0.03), (0.017, 0.015)) });
            K.Bed(NoiseColor.White, new BedOptions { Type = BiquadType.Bandpass, F = 1800, Q = 3, Gain = 0.004, GLfo = Lfo((0.07, 0.003)) });
            K.Drone(OscType.Sine, 46.25, 0.016);
            K.Drone(OscType.Sawtooth, 61.74, 0.006, lp: 140);
            K.Every(13, 26, (t, k, r) =>
            {
                K.Tick(t, 2400, 1, 0.003, 0.09);
                K.Rumble(t + 0.25 + 0.6 * r(), 0.07, 180, 0.4 + 0.5 * r(), 3.2 + 1.8 * r());
            });
        }

        private static readonly double[] Layer7Glides = { 196, 174.61, 155.56 };

        /// <summary>
        /// js: scenes/audio/Audio.js:950 7 果てなき世界 — the void of miasma: pressure, two low tones beating against each other, the
        /// spring welling up and down, the miasma exhaling in slow falling tones. The darkest place.
        /// </summary>
        public static void Layer7(AmbienceKit K)
        {
            K.Bed(NoiseColor.Brown, new BedOptions { Type = BiquadType.Lowpass, F = 120, Gain = 0.09, GLfo = Lfo((0.023, 0.03)) });
            K.Drone(OscType.Sine, 55, 0.012);
            K.Drone(OscType.Sine, 55.35, 0.012);
            K.Drone(OscType.Sine, 82.6, 0.004);
            K.Drone(OscType.Sawtooth, 27.5, 0.01, lp: 90);
            K.Drone(OscType.Sine, 164.81, 0.004, gLfo: Lfo((0.031, 0.003)));
            K.Drone(OscType.Sine, 165.3, 0.004);
            K.Bed(NoiseColor.Pink, new BedOptions { Type = BiquadType.Bandpass, F = 400, Q = 1.5, Gain = 0.03, FLfo = Lfo((0.06, 240)) });
            K.Every(16, 28, (t, k, r) => { double f = Layer7Glides[k % 3]; K.Glide(t, f, f * 0.94, 0.009, 3.4); });
        }
    }
}
