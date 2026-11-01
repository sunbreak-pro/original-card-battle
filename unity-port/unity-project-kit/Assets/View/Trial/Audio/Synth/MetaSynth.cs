// js: scenes/audio/Audio.js:309-576: the meta synth's primitives (G, F, O, N, pk, env, swellEnv, send) and its 26 timbres
// (the 24 labelled ones plus thud and swish), line by line in the same node-creation and param-call order.
// Each timbre takes (t, pitch Hz, dur s, vol, tone 0..1, o) with o = { pan, dst, surface, pitchMul }.
// The synthesis half of play() (Audio.js:722-736: counters, the step's pan and surface, the ±3 % alternation, pitch and
// volume) is PlayPreset; the rules before it (mute, unlock, battle names, gaps) belong to the caller (TrialAudio).
using System;
using System.Collections.Generic;

namespace Trial.Audio.Synth
{
    /// <summary>js: the <c>o</c> of a meta voice: <c>{ dst, pan, surface, pitchMul }</c>.</summary>
    public sealed class VoiceRouting
    {
        /// <summary>Connect into this node only (an ambience layer's mix); no effects bus, no pan.</summary>
        public AudioNode Dst;
        public double? Pan;
        public string Surface;
        public double? PitchMul;

        public VoiceRouting Clone() => (VoiceRouting)MemberwiseClone();
    }

    public sealed class MetaSynth
    {
        public AudioContextLite Ctx { get; }
        /// <summary>The effects bus (dry) and the room's input; voices <c>send</c> to both.</summary>
        public AudioNode Fx { get; }
        public AudioNode FxWet { get; }
        public NoiseBank Bufs { get; }

        /// <summary>js: <c>noiseTurn</c>, one counter shared by every noise burst (meta and ambience).</summary>
        public int NoiseTurn { get; set; }
        /// <summary>js: <c>turn</c>, the meta play counter (separate from the design system's).</summary>
        public int Turn { get; set; }
        /// <summary>js: <c>stepTurn</c>, the footstep counter (left / right).</summary>
        public int StepTurn { get; set; }
        /// <summary>js: <c>surface</c>, the current place's ground (setAmbience sets it).</summary>
        public string Surface { get; set; } = "stone";
        /// <summary>js: <c>errors</c>: voices that threw (skipped, not fatal).</summary>
        public int Errors { get; set; }

        // js: scenes/audio/Audio.js:281 (the parts of ensure() a voice needs)
        public MetaSynth(AudioContextLite ctx, AudioNode fx, AudioNode fxWet, NoiseBank bufs)
        {
            Ctx = ctx;
            Fx = fx;
            FxWet = fxWet;
            Bufs = bufs;
        }

        /// <summary>js: scenes/audio/Audio.js:342 Object.keys(SYN), in order.</summary>
        public static readonly string[] Timbres =
        {
            "paper", "slide", "wood", "thud", "seal", "swish", "step", "page", "close", "quill", "exhale", "drill", "settle", "ember",
            "pouch", "swell", "petrify", "toll", "hum", "fall", "tuck", "expire", "relief", "kindle", "nameSeal", "bow",
        };

        // ---- primitives (Audio.js:309-338) ---------------------------------------------------------------------------

        // js: scenes/audio/Audio.js:309
        public GainNode G(double v = 1)
        {
            var g = Ctx.CreateGain(); g.Gain.Value = v; return g;
        }

        // js: scenes/audio/Audio.js:310
        public BiquadFilterNode F(BiquadType type, double f, double q = 0.7)
        {
            var b = Ctx.CreateBiquadFilter(); b.Type = type; b.Frequency.Value = f; b.Q.Value = q; return b;
        }

        // js: scenes/audio/Audio.js:311
        public OscillatorNode O(OscType type, double f, double t, double dur)
        {
            var o = Ctx.CreateOscillator(); o.Type = type; o.Frequency.SetValueAtTime(f, t);
            o.Start(t); o.Stop(t + dur + 0.05); return o;
        }

        // js: scenes/audio/Audio.js:315
        public AudioBufferSourceNode N(double t, double dur, NoiseColor color = NoiseColor.White)
        {
            // Each burst starts at a different point of the buffer (a counter, not a random number).
            var s = Ctx.CreateBufferSource(); s.Buffer = Bufs.Get(color); s.Loop = true;
            s.Start(t, (NoiseTurn++ * 0.137) % (AudioBuffers.NoiseSeconds - 0.5)); s.Stop(t + dur + 0.05); return s;
        }

        // js: scenes/audio/Audio.js:320
        public static double Pk(double v) => Math.Max(0.0002, v);

        // js: scenes/audio/Audio.js:321
        public static void Env(GainNode g, double t, double a, double peak, double dur)
        {
            g.Gain.SetValueAtTime(0.0001, t);
            g.Gain.ExponentialRampToValueAtTime(Math.Max(0.0002, peak), t + a);
            g.Gain.ExponentialRampToValueAtTime(0.0001, t + Math.Max(a + 0.01, dur));
        }

        /// <summary>js: scenes/audio/Audio.js:327 swellEnv: rise linearly to peak at t+a, then decay exponentially to silence at t+dur.</summary>
        public static void SwellEnv(GainNode g, double t, double a, double peak, double dur)
        {
            g.Gain.SetValueAtTime(0.0001, t);
            g.Gain.LinearRampToValueAtTime(Math.Max(0.0002, peak), t + a);
            g.Gain.ExponentialRampToValueAtTime(0.0001, t + Math.Max(a + 0.02, dur));
        }

        /// <summary>js: scenes/audio/Audio.js:333 send: into an ambience layer (o.dst), or to the effects bus, dry and wet.</summary>
        public void Send(AudioNode node, VoiceRouting o)
        {
            if (o != null && o.Dst != null) { node.Connect(o.Dst); return; }
            AudioNode n = node;
            if (o != null && o.Pan.HasValue && o.Pan.Value != 0 && !double.IsNaN(o.Pan.Value))
            {
                var p = Ctx.CreateStereoPanner(); p.Pan.Value = o.Pan.Value; node.Connect(p); n = p;
            }
            n.Connect(Fx); n.Connect(FxWet);
        }

        // ---- meta timbres (Audio.js:342-576) ---------------------------------------------------------------------------
        // Ports of the battle synth's paper / slide / wood / thud / seal / swish, so meta and battle match.

        // js: scenes/audio/Audio.js:344
        public void Paper(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var n = N(t, d); var bp = F(BiquadType.Bandpass, p, 0.8 + tone * 2); var hp = F(BiquadType.Highpass, 500); var g = G(0);
            n.Connect(bp); bp.Connect(hp); hp.Connect(g); Env(g, t, 0.004, v, d); Send(g, o);
        }

        // js: scenes/audio/Audio.js:348
        public void Slide(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var n = N(t, d); var bp = F(BiquadType.Bandpass, p, 0.6); var g = G(0);
            bp.Frequency.SetValueAtTime(p * 1.3, t); bp.Frequency.ExponentialRampToValueAtTime(p * 0.7, t + d);
            n.Connect(bp); bp.Connect(g);
            g.Gain.SetValueAtTime(0.0001, t); g.Gain.LinearRampToValueAtTime(Pk(v), t + d * 0.3); g.Gain.ExponentialRampToValueAtTime(0.0001, t + d);
            Send(g, o);
        }

        // js: scenes/audio/Audio.js:355
        public void Wood(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var x = O(OscType.Triangle, p, t, d); var g = G(0);
            x.Frequency.ExponentialRampToValueAtTime(p * 0.82, t + d);
            x.Connect(g); Env(g, t, 0.002, v, d); Send(g, o);
            var n = N(t, 0.02); var hp = F(BiquadType.Highpass, 2500); var g2 = G(0); n.Connect(hp); hp.Connect(g2); Env(g2, t, 0.001, v * 0.35, 0.018); Send(g2, o);
        }

        // js: scenes/audio/Audio.js:361
        public void Thud(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var x = O(OscType.Sine, p, t, d); var g = G(0);
            x.Frequency.ExponentialRampToValueAtTime(p * 0.55, t + d);
            x.Connect(g); Env(g, t, 0.003, v, d); Send(g, o);
        }

        // js: scenes/audio/Audio.js:366
        public void Seal(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            Thud(t, p, d * 0.7, v, tone, o);
            Wood(t + 0.012, 1500, 0.025, v * 0.5, tone, o);
        }

        // js: scenes/audio/Audio.js:370
        public void Swish(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var n = N(t, d); var bp = F(BiquadType.Bandpass, p * 0.5, 1.4); var g = G(0);
            bp.Frequency.SetValueAtTime(p * 0.45, t); bp.Frequency.ExponentialRampToValueAtTime(p * 1.7, t + d);
            n.Connect(bp); bp.Connect(g);
            g.Gain.SetValueAtTime(0.0001, t); g.Gain.ExponentialRampToValueAtTime(Math.Max(0.0002, v), t + d * 0.6); g.Gain.ExponentialRampToValueAtTime(0.0001, t + d);
            Send(g, o);
        }

        /// <summary>js: scenes/audio/Audio.js:379 step: a damp heel thud and a scuff of the place's ground (SURFACES).</summary>
        public void Step(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            Surface s = o != null && o.Surface != null && SoundData.Surfaces.TryGetValue(o.Surface, out Surface found) ? found : SoundData.Surfaces["stone"];
            Thud(t, p * s.Thud, d * 0.55, v * s.Heel, tone, o);
            var n = N(t + 0.008, d, NoiseColor.Pink); var f = F(s.Type, s.F * (0.7 + tone * 0.6), s.Q); var g = G(0);
            n.Connect(f); f.Connect(g); Env(g, t + 0.008, 0.006, v * s.Scuff, d); Send(g, o);
            if (s.Crackle != 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    double at = t + 0.016 + i * 0.027; var c = N(at, 0.012); var b = F(BiquadType.Bandpass, s.F * 1.3, 2); var gc = G(0);
                    c.Connect(b); b.Connect(gc); Env(gc, at, 0.001, v * s.Crackle * (1 - i * 0.25), 0.012); Send(gc, o);
                }
            }
            if (s.Wet != 0)
            {
                var w = N(t + 0.02, 0.035); var b = F(BiquadType.Bandpass, 1100, 3); var gw = G(0);
                w.Connect(b); b.Connect(gw); Env(gw, t + 0.02, 0.002, v * s.Wet, 0.035); Send(gw, o);
            }
            if (s.Ring != 0)
            {
                var r = O(OscType.Sine, 1900, t, 0.3); var gr = G(0);
                r.Connect(gr); Env(gr, t + 0.004, 0.003, v * s.Ring, 0.28); Send(gr, o);
            }
        }

        /// <summary>js: scenes/audio/Audio.js:400 page: a page lifts and turns: noise sweeping up with a flutter of the paper edge.</summary>
        public void Page(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var n = N(t, d); var bp = F(BiquadType.Bandpass, p * 0.6, 0.9 + tone); var am = G(0.6); var g = G(0);
            bp.Frequency.SetValueAtTime(p * 0.6, t); bp.Frequency.ExponentialRampToValueAtTime(p * 1.5, t + d * 0.7);
            var lfo = O(OscType.Sine, 19, t, d); var depth = G(0.4); lfo.Connect(depth); depth.Connect(am.Gain);
            n.Connect(bp); bp.Connect(am); am.Connect(g);
            g.Gain.SetValueAtTime(0.0001, t); g.Gain.LinearRampToValueAtTime(Pk(v), t + d * 0.35); g.Gain.ExponentialRampToValueAtTime(0.0001, t + d);
            Send(g, o);
        }

        /// <summary>js: scenes/audio/Audio.js:409 close: the book shuts: the pages slide down (action) and the covers meet softly (result).</summary>
        public void Close(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var n = N(t, d); var bp = F(BiquadType.Bandpass, p * 1.2, 0.7); var g = G(0);
            bp.Frequency.SetValueAtTime(p * 1.2, t); bp.Frequency.ExponentialRampToValueAtTime(p * 0.5, t + d * 0.8);
            n.Connect(bp); bp.Connect(g);
            g.Gain.SetValueAtTime(0.0001, t); g.Gain.LinearRampToValueAtTime(Pk(v * 0.7), t + d * 0.25); g.Gain.ExponentialRampToValueAtTime(0.0001, t + d * 0.85);
            Send(g, o);
            Thud(t + d * 0.75, 140, 0.12, v * 0.55, tone, o);
        }

        /// <summary>js: scenes/audio/Audio.js:418 quill: three short strokes of a dry brush: a line written into the journal.</summary>
        public void Quill(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            double s = d / 3;
            for (int i = 0; i < 3; i++)
            {
                double at = t + i * s * 0.9; var n = N(at, s); var b = F(BiquadType.Bandpass, p * (1.08 - i * 0.07), 2.5 + tone * 2); var g = G(0);
                b.Frequency.SetValueAtTime(p * (1.08 - i * 0.07), at); b.Frequency.LinearRampToValueAtTime(p * (0.9 - i * 0.07), at + s * 0.8);
                n.Connect(b); b.Connect(g); SwellEnv(g, at, s * 0.3, v * (1 - i * 0.18), s * 0.85); Send(g, o);
            }
        }

        /// <summary>js: scenes/audio/Audio.js:427 exhale: a long breath out: noise falling in pitch.</summary>
        public void Exhale(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var n = N(t, d, NoiseColor.Pink); var b = F(BiquadType.Bandpass, p * 1.25, 0.7); var l = F(BiquadType.Lowpass, 1400 + tone * 1200); var g = G(0);
            b.Frequency.SetValueAtTime(p * 1.25, t); b.Frequency.ExponentialRampToValueAtTime(p * 0.55, t + d);
            n.Connect(b); b.Connect(l); l.Connect(g); SwellEnv(g, t, d * 0.18, v, d); Send(g, o);
        }

        /// <summary>js: scenes/audio/Audio.js:433 drill: training: a practice swing (action) and the knock on the post (result).</summary>
        public void Drill(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            Swish(t, p, d, v, tone, o);
            double k = t + d * 0.92;
            Wood(k, 300, 0.06, v * 0.8, tone, o);
            Thud(k, 140, 0.12, v * 0.6, tone, o);
        }

        /// <summary>js: scenes/audio/Audio.js:440 settle: something is set down and the dust settles: one low thunk with a soft tail.</summary>
        public void Settle(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            Thud(t, p, d * 0.5, v, tone, o);
            var n = N(t + 0.02, d, NoiseColor.Pink); var l = F(BiquadType.Lowpass, 500 + tone * 500); var g = G(0);
            n.Connect(l); l.Connect(g); SwellEnv(g, t + 0.02, 0.04, v * 0.3, d); Send(g, o);
        }

        private static readonly double[][] EmberPartials = { new[] { 1, 1, 1.0 }, new[] { 2, 0.28, 0.6 }, new[] { 3, 0.1, 0.4 } };

        /// <summary>js: scenes/audio/Audio.js:446 ember: a warm, low-lit glow (the wick, 灯芯): a soft chime with an octave and a breath of flame under it.</summary>
        public void Ember(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var lp = F(BiquadType.Lowpass, p * (2.5 + tone * 2), 0.5); var @out = G(1);
            lp.Connect(@out); Send(@out, o);
            foreach (double[] e in EmberPartials)
            {
                double m = e[0], a = e[1], dd = e[2];
                var x = O(OscType.Sine, p * m, t, d * dd); var g = G(0); x.Connect(g); Env(g, t, 0.02, v * a, d * dd); g.Connect(lp);
            }
            var n = N(t, d * 0.6, NoiseColor.Pink); var l = F(BiquadType.Lowpass, 600); var g2 = G(0);
            n.Connect(l); l.Connect(g2); SwellEnv(g2, t, d * 0.25, v * 0.25, d * 0.6); Send(g2, o);
        }

        /// <summary>js: scenes/audio/Audio.js:456 pouch: a cloth pouch opens (action) and the thing is taken (result).</summary>
        public void Pouch(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var n = N(t, 0.13); var b = F(BiquadType.Bandpass, p, 0.7); var am = G(0.6); var g = G(0);
            var lfo = O(OscType.Sine, 28, t, 0.13); var depth = G(0.4); lfo.Connect(depth); depth.Connect(am.Gain);
            n.Connect(b); b.Connect(am); am.Connect(g); SwellEnv(g, t, 0.03, v * 0.7, 0.13); Send(g, o);
            Thud(t + Math.Min(0.12, d * 0.5), 170, Math.Max(0.08, d - 0.12), v * 0.6, tone, o);
        }

        private struct Partial
        {
            public OscType Type;
            public double Ratio, Amp;

            public Partial(OscType type, double ratio, double amp)
            {
                Type = type; Ratio = ratio; Amp = amp;
            }
        }

        /// <summary>js: scenes/audio/Audio.js:463 swell: a low swell (the miasma rises past a line): two beating low sines, an octave above and a dark rumble.</summary>
        public void Swell(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var g = G(0);
            foreach (Partial e in new[] { new Partial(OscType.Sine, p, 0.45), new Partial(OscType.Sine, p * 1.012, 0.45), new Partial(OscType.Triangle, p * 2.02, 0.6), new Partial(OscType.Sine, p * 3.01, 0.12) })
            {
                var x = O(e.Type, e.Ratio, t, d); var ga = G(e.Amp); x.Connect(ga); ga.Connect(g);
            }
            var nb = N(t, d, NoiseColor.Brown); var nl = F(BiquadType.Lowpass, 180 + tone * 300, 0.8); var gn = G(0.6);
            nb.Connect(nl); nl.Connect(gn); gn.Connect(g);
            SwellEnv(g, t, d * 0.55, v, d); Send(g, o);
        }

        /// <summary>js: scenes/audio/Audio.js:473 petrify: the body hardens: muffled crystal ticks closing up (action), under a low swell that stops short (result).</summary>
        public void Petrify(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var lp = F(BiquadType.Lowpass, 2200 + tone * 1200, 0.7); var gl = G(1);
            lp.Connect(gl); Send(gl, o);
            double at = t;
            for (int i = 0; i < 9; i++)
            {
                var n = N(at, 0.012); var b = F(BiquadType.Bandpass, 1700 + (i % 3) * 350, 3); var gt = G(0);
                n.Connect(b); b.Connect(gt); Env(gt, at, 0.001, v * 0.22 * (0.6 + i * 0.05), 0.012); gt.Connect(lp);
                at += 0.32 * Math.Pow(0.78, i);
            }
            var g = G(0);
            foreach (Partial e in new[] { new Partial(OscType.Sine, p, 0.45), new Partial(OscType.Sine, p * 1.012, 0.45), new Partial(OscType.Triangle, p * 2.02, 0.6), new Partial(OscType.Sine, p * 3.01, 0.12) })
            {
                var x = O(e.Type, e.Ratio, t, d); var ga = G(e.Amp); x.Connect(ga); ga.Connect(g);
            }
            var nb = N(t, d, NoiseColor.Brown); var nl = F(BiquadType.Lowpass, 220, 0.8); var gn = G(0.7);
            nb.Connect(nl); nl.Connect(gn); gn.Connect(g);
            g.Gain.SetValueAtTime(0.0001, t);
            g.Gain.LinearRampToValueAtTime(v, t + d * 0.62);
            g.Gain.LinearRampToValueAtTime(0.0001, t + d * 0.66);
            Send(g, o);
        }

        private static readonly double[][] TollPartials =
        {
            new[] { 0.5, 0.5, 1 }, new[] { 1, 1, 0.8 }, new[] { 1.19, 0.45, 0.55 }, new[] { 1.5, 0.3, 0.45 }, new[] { 2, 0.35, 0.35 }, new[] { 2.66, 0.16, 0.25 },
        };

        /// <summary>js: scenes/audio/Audio.js:494 toll: a damp low bell, struck once: inharmonic partials under a low lid.</summary>
        public void Toll(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var lp = F(BiquadType.Lowpass, p * (5 + tone * 6), 0.5); var @out = G(1);
            lp.Connect(@out); Send(@out, o);
            foreach (double[] e in TollPartials)
            {
                double m = e[0], a = e[1], dd = e[2];
                var x = O(OscType.Sine, p * m, t, d * dd); var g = G(0); x.Connect(g); Env(g, t, 0.004, v * a, d * dd); g.Connect(lp);
            }
            var n = N(t, 0.05, NoiseColor.Pink); var nl = F(BiquadType.Lowpass, 900); var ng = G(0);
            n.Connect(nl); nl.Connect(ng); Env(ng, t, 0.002, v * 0.3, 0.05); Send(ng, o);
        }

        private static readonly double[][] HumPartials = { new[] { 1, 1.0 }, new[] { 1.5, 0.55 }, new[] { 2, 0.25 } };

        /// <summary>js: scenes/audio/Audio.js:504 hum: a low warm chord (root, fifth, octave) that rises slowly and fades.</summary>
        public void Hum(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var lp = F(BiquadType.Lowpass, p * (4 + tone * 6), 0.5); var g = G(0);
            foreach (double[] e in HumPartials) { double m = e[0], a = e[1]; var x = O(OscType.Sine, p * m, t, d); var gg = G(a); x.Connect(gg); gg.Connect(lp); }
            lp.Connect(g); SwellEnv(g, t, d * 0.4, v, d); Send(g, o);
        }

        /// <summary>js: scenes/audio/Audio.js:510 fall: a deep string falling: the pitch and the lid sink together, with a low rumble of depth.</summary>
        public void Fall(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var lp = F(BiquadType.Lowpass, p * 10 * (0.6 + tone), 1.0); var g = G(0);
            lp.Frequency.SetValueAtTime(p * 10 * (0.6 + tone), t); lp.Frequency.ExponentialRampToValueAtTime(p * 2.2, t + d * 0.85);
            foreach (Partial e in new[] { new Partial(OscType.Sawtooth, p, 1), new Partial(OscType.Triangle, p * 1.004, 1), new Partial(OscType.Sine, p * 0.5, 0.6) })
            {
                double f = e.Ratio;
                var x = O(e.Type, f, t, d); var ga = G(e.Amp);
                x.Frequency.ExponentialRampToValueAtTime(f * 0.42, t + d * 0.9);
                x.Connect(ga); ga.Connect(lp);
            }
            lp.Connect(g); Env(g, t, 0.05, v, d); Send(g, o);
            var n = N(t, d, NoiseColor.Brown); var nl = F(BiquadType.Lowpass, 160); var gn = G(0);
            n.Connect(nl); nl.Connect(gn); SwellEnv(gn, t, d * 0.3, v * 0.4, d); Send(gn, o);
        }

        /// <summary>js: scenes/audio/Audio.js:523 tuck: a card slides into the deck (action) and the deck is squared with a tap (result).</summary>
        public void Tuck(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            Slide(t, p, Math.Min(0.15, d), v * 0.8, tone, o);
            Wood(t + Math.Min(0.13, d * 0.6), p * 0.48, 0.035, v * 0.7, tone, o);
        }

        /// <summary>js: scenes/audio/Audio.js:528 expire: the last heartbeat, slower than the battle's (action), then a low tone sinking into silence (result).</summary>
        public void Expire(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            Thud(t, p, 0.18, v * 0.9, tone, o);
            Thud(t + 0.24, p * 0.9, 0.2, v * 0.6, tone, o);
            double t2 = t + 0.7, d2 = Math.Max(0.5, d - 0.7);
            var lp = F(BiquadType.Lowpass, p * 9 * (0.6 + tone), 0.6); var g = G(0);
            foreach (Partial e in new[] { new Partial(OscType.Triangle, p * 2, 0), new Partial(OscType.Sine, p, 0) })
            {
                double f = e.Ratio;
                var x = O(e.Type, f, t2, d2); x.Frequency.ExponentialRampToValueAtTime(f * 0.7, t2 + d2); x.Connect(lp);
            }
            lp.Connect(g); SwellEnv(g, t2, 0.6, v * 0.8, d2); Send(g, o);
        }

        /// <summary>js: scenes/audio/Audio.js:539 relief: a long breath out (action) and the wick's small warm glow (result).</summary>
        public void Relief(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            Exhale(t, 700, d * 0.6, v, tone, o);
            Ember(t + d * 0.42, p, d * 0.6, v * 0.8, tone, o);
        }

        /// <summary>js: scenes/audio/Audio.js:544 kindle: a lamp is lit: the strike (action), then the flame catches with a warm low glow (result).</summary>
        public void Kindle(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var n = N(t, 0.09); var b = F(BiquadType.Bandpass, p, 1.4); var g = G(0);
            b.Frequency.SetValueAtTime(p * 0.7, t); b.Frequency.ExponentialRampToValueAtTime(p * 1.3, t + 0.07);
            n.Connect(b); b.Connect(g); Env(g, t, 0.003, v * 0.7, 0.08); Send(g, o);
            double t2 = t + 0.11, d2 = Math.Max(0.3, d - 0.11);
            var f = N(t2, d2, NoiseColor.Pink); var fl = F(BiquadType.Lowpass, 520 + tone * 300, 0.7); var gf = G(0);
            f.Connect(fl); fl.Connect(gf); SwellEnv(gf, t2, 0.25, v * 0.8, d2); Send(gf, o);
            var w = O(OscType.Sine, 110, t2, d2); var w2 = O(OscType.Sine, 165, t2, d2); var g5 = G(0.5); var gw = G(0);
            w.Connect(gw); w2.Connect(g5); g5.Connect(gw); SwellEnv(gw, t2, d2 * 0.4, v * 0.25, d2); Send(gw, o);
        }

        /// <summary>js: scenes/audio/Audio.js:555 nameSeal: 襲名: the name is stamped with a heavy seal (action) and a low string answers (result).</summary>
        public void NameSeal(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            Seal(t, 120, 0.16, v, tone, o);
            double t2 = t + 0.22, d2 = Math.Max(0.4, d - 0.22);
            var lp = F(BiquadType.Lowpass, p * 10, 1.1); var g = G(0);
            lp.Frequency.SetValueAtTime(p * 10, t2); lp.Frequency.ExponentialRampToValueAtTime(p * 1.8, t2 + d2 * 0.7);
            var a = O(OscType.Sawtooth, p, t2, d2); var b = O(OscType.Triangle, p * 1.003, t2, d2);
            a.Connect(lp); b.Connect(lp); lp.Connect(g); Env(g, t2, 0.02, v * 0.6, d2); Send(g, o);
        }

        /// <summary>js: scenes/audio/Audio.js:564 bow: a bowed low string: a slow attack, the lid opening and closing, and a breath of bow hair.</summary>
        public void Bow(double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            var lp = F(BiquadType.Lowpass, p * 3, 1.1); var g = G(0);
            lp.Frequency.SetValueAtTime(p * 3, t); lp.Frequency.LinearRampToValueAtTime(p * (5 + tone * 6), t + d * 0.4);
            lp.Frequency.ExponentialRampToValueAtTime(p * 2.5, t + d);
            var a = O(OscType.Sawtooth, p, t, d); var b = O(OscType.Sawtooth, p * 1.005, t, d); var c = O(OscType.Triangle, p * 0.5, t, d); var gc = G(0.5);
            a.Connect(lp); b.Connect(lp); c.Connect(gc); gc.Connect(lp); lp.Connect(g);
            g.Gain.SetValueAtTime(0.0001, t); g.Gain.LinearRampToValueAtTime(Pk(v), t + d * 0.35);
            g.Gain.LinearRampToValueAtTime(Pk(v * 0.8), t + d * 0.6); g.Gain.ExponentialRampToValueAtTime(0.0001, t + d);
            Send(g, o);
            var n = N(t, d, NoiseColor.Pink); var nb = F(BiquadType.Bandpass, p * 9, 1.2); var gn = G(0);
            n.Connect(nb); nb.Connect(gn); SwellEnv(gn, t, d * 0.35, v * 0.12, d * 0.9); Send(gn, o);
        }

        /// <summary>js: <c>SYN[timbre]</c>: true when the synth has the timbre.</summary>
        public static bool Has(string timbre) => timbre != null && Array.IndexOf(Timbres, timbre) >= 0;

        /// <summary>js: <c>SYN[timbre](t, p, d, v, tone, o)</c>; false for a timbre the synth does not have.</summary>
        public bool Synth(string timbre, double t, double p, double d, double v, double tone, VoiceRouting o)
        {
            switch (timbre)
            {
                case "paper": Paper(t, p, d, v, tone, o); return true;
                case "slide": Slide(t, p, d, v, tone, o); return true;
                case "wood": Wood(t, p, d, v, tone, o); return true;
                case "thud": Thud(t, p, d, v, tone, o); return true;
                case "seal": Seal(t, p, d, v, tone, o); return true;
                case "swish": Swish(t, p, d, v, tone, o); return true;
                case "step": Step(t, p, d, v, tone, o); return true;
                case "page": Page(t, p, d, v, tone, o); return true;
                case "close": Close(t, p, d, v, tone, o); return true;
                case "quill": Quill(t, p, d, v, tone, o); return true;
                case "exhale": Exhale(t, p, d, v, tone, o); return true;
                case "drill": Drill(t, p, d, v, tone, o); return true;
                case "settle": Settle(t, p, d, v, tone, o); return true;
                case "ember": Ember(t, p, d, v, tone, o); return true;
                case "pouch": Pouch(t, p, d, v, tone, o); return true;
                case "swell": Swell(t, p, d, v, tone, o); return true;
                case "petrify": Petrify(t, p, d, v, tone, o); return true;
                case "toll": Toll(t, p, d, v, tone, o); return true;
                case "hum": Hum(t, p, d, v, tone, o); return true;
                case "fall": Fall(t, p, d, v, tone, o); return true;
                case "tuck": Tuck(t, p, d, v, tone, o); return true;
                case "expire": Expire(t, p, d, v, tone, o); return true;
                case "relief": Relief(t, p, d, v, tone, o); return true;
                case "kindle": Kindle(t, p, d, v, tone, o); return true;
                case "nameSeal": NameSeal(t, p, d, v, tone, o); return true;
                case "bow": Bow(t, p, d, v, tone, o); return true;
                default: return false;
            }
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:722-736, the synthesis half of play() after its gate (mute, unlock, gap): <c>turn++</c>; for
        /// <c>step</c>, <c>stepTurn++</c>, the pan alternating +0.12 / −0.12 (first step right) unless given, and the current
        /// surface unless given; the ±3 % alternation for paper, wood and step; <c>t = now + max(0, delayMs)/1000</c>;
        /// <c>pitch × (1 + wob) × (pitchMul || 1)</c>; <c>vol × (mult ?? 1)</c>. A voice that throws counts in
        /// <see cref="Errors"/> and returns false.
        /// </summary>
        public bool PlayPreset(SoundPreset pr, double now, double delayMs = 0, double? mult = null, VoiceRouting options = null)
        {
            Turn++;
            if (pr == null || !Has(pr.Timbre)) return false;
            VoiceRouting o = options != null ? options.Clone() : new VoiceRouting();
            if (pr.Timbre == "step")
            {
                StepTurn++;
                if (o.Pan == null) o.Pan = SoundRules.StepPan(StepTurn); // left, right, left ...
                if (string.IsNullOrEmpty(o.Surface)) o.Surface = Surface;
            }
            // Alternate the pitch by ±3% so repeats do not stack into one tone (as the battle synth does).
            double wob = pr.Timbre == "paper" || pr.Timbre == "wood" || pr.Timbre == "step" ? SoundRules.Wobble(Turn) : 0;
            double t = now + Math.Max(0, double.IsNaN(delayMs) ? 0 : delayMs) / 1000;
            double pitchMul = o.PitchMul.HasValue && o.PitchMul.Value != 0 && !double.IsNaN(o.PitchMul.Value) ? o.PitchMul.Value : 1;
            double pitch = pr.Pitch * (1 + wob) * pitchMul;
            try
            {
                Synth(pr.Timbre, t, pitch, pr.Dur / 1000, pr.Vol * (mult ?? 1), pr.Tone, o);
            }
            catch (Exception)
            {
                Errors++;
                return false;
            }
            return true;
        }
    }
}
