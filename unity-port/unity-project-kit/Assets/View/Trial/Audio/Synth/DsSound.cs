// js: vendor/ds/bundle.js:704-840 Sound(): the design system's battle synth, line by line in the same node-creation and
// param-call order. Sound — synthesised with WebAudio, no audio files. One named preset per event.
// Rule (battle_ui_ux_v2.md §5.20): at most two sounds per action — the action sound and the result sound.
// No sound at the moment a number floats; the step-in is silent. Dark, quiet, damp: no fanfare, no bright beeps.
//
// Two ways to use it: `new DsSound(ctx)` builds its own graph on first unlock (ensure(), as the JS does: compressor,
// out, the 1.4 s damp room behind a low-pass, the 1 s noise); `DsSound.OnBuses(ctx, out, wet, noise)` plays into buses
// the caller owns (PresetRenderer).
using System;
using System.Collections.Generic;

namespace Trial.Audio.Synth
{
    public sealed class DsSound
    {
        public bool Muted;
        public double Master = 0.8, Reverb = 0.18;
        public Dictionary<string, SoundPreset> Presets = SoundData.DsSoundDefaults();

        private readonly AudioContextLite ctx;
        private GainNode @out, wet;
        private AudioBuffer noise;
        private bool unlocked;
        private double curTone = 0.5;
        private readonly Dictionary<string, double> lastAt = new Dictionary<string, double>();

        /// <summary>js: <c>turn</c>, the play counter (the noise offset and the ±3 % alternation read it).</summary>
        public int Turn { get; set; }
        public AudioContextLite Context => ctx;
        public bool Unlocked => unlocked;
        /// <summary>The bus every voice goes to dry (<c>out</c>), and the room's input (<c>wet</c>); null before ensure().</summary>
        public GainNode Out => @out;
        public GainNode Wet => wet;

        // js: vendor/ds/bundle.js:704
        public DsSound(AudioContextLite context)
        {
            ctx = context;
        }

        /// <summary>Voices into the caller's buses; already ensured and unlocked.</summary>
        public static DsSound OnBuses(AudioContextLite context, GainNode outBus, GainNode wetBus, AudioBuffer noiseBuffer)
        {
            return new DsSound(context) { @out = outBus, wet = wetBus, noise = noiseBuffer, unlocked = true };
        }

        // js: vendor/ds/bundle.js:707
        private bool Ensure()
        {
            if (@out != null) return true;
            var comp = ctx.CreateDynamicsCompressor(); comp.Threshold.Value = -18; comp.Ratio.Value = 4;
            @out = ctx.CreateGain(); @out.Gain.Value = Master;
            @out.Connect(comp); comp.Connect(ctx.Destination);
            // A short damp room: decaying noise as the impulse, mostly low frequencies.
            AudioBuffer ir = AudioBuffers.MakeDampRoom(ctx.SampleRate);
            var conv = ctx.CreateConvolver(); conv.Buffer = ir;
            var lp = ctx.CreateBiquadFilter(); lp.Type = BiquadType.Lowpass; lp.Frequency.Value = 2400;
            wet = ctx.CreateGain(); wet.Gain.Value = Reverb;
            wet.Connect(lp); lp.Connect(conv); conv.Connect(@out);
            noise = AudioBuffers.MakeDsNoise(ctx.SampleRate);
            return true;
        }

        // js: vendor/ds/bundle.js:732
        public void Unlock()
        {
            unlocked = true;
            Ensure();
        }

        // js: vendor/ds/bundle.js:733
        public void SetMaster(double v)
        {
            Master = v;
            if (@out != null) @out.Gain.Value = v;
        }

        // js: vendor/ds/bundle.js:734
        public void SetReverb(double v)
        {
            Reverb = v;
            if (wet != null) wet.Gain.Value = v;
        }

        // js: vendor/ds/bundle.js:735
        private void Dest(AudioNode g)
        {
            g.Connect(@out); g.Connect(wet);
        }

        // js: vendor/ds/bundle.js:736
        private static void Env(GainNode g, double t, double a, double peak, double dur)
        {
            g.Gain.SetValueAtTime(0.0001, t);
            g.Gain.ExponentialRampToValueAtTime(Math.Max(0.0002, peak), t + a);
            g.Gain.ExponentialRampToValueAtTime(0.0001, t + Math.Max(a + 0.01, dur));
        }

        // js: vendor/ds/bundle.js:741
        private AudioBufferSourceNode NoiseSrc(double t, double dur)
        {
            var s = ctx.CreateBufferSource(); s.Buffer = noise; s.Loop = true; s.Start(t, (Turn * 0.137) % 0.8); s.Stop(t + dur + 0.05); return s;
        }

        // js: vendor/ds/bundle.js:742
        private OscillatorNode Osc(OscType type, double f, double t, double dur)
        {
            var o = ctx.CreateOscillator(); o.Type = type; o.Frequency.SetValueAtTime(f, t); o.Start(t); o.Stop(t + dur + 0.05); return o;
        }

        // js: vendor/ds/bundle.js:743 (Q.value = q || 0.7)
        private BiquadFilterNode Filt(BiquadType type, double f, double q = 0)
        {
            var b = ctx.CreateBiquadFilter(); b.Type = type; b.Frequency.Value = f; b.Q.Value = q != 0 && !double.IsNaN(q) ? q : 0.7; return b;
        }

        private double PTone() => curTone;

        // ---- SYN (vendor/ds/bundle.js:744-822) ------------------------------------------------------------------------

        // js: vendor/ds/bundle.js:745
        public void Paper(double t, double p, double d, double v)
        {
            var n = NoiseSrc(t, d); var bp = Filt(BiquadType.Bandpass, p, 0.8 + PTone() * 2); var hp = Filt(BiquadType.Highpass, 500); var g = ctx.CreateGain();
            n.Connect(bp); bp.Connect(hp); hp.Connect(g); Env(g, t, 0.004, v, d); Dest(g);
        }

        // js: vendor/ds/bundle.js:749
        public void Slide(double t, double p, double d, double v)
        {
            var n = NoiseSrc(t, d); var bp = Filt(BiquadType.Bandpass, p, 0.6); var g = ctx.CreateGain();
            bp.Frequency.SetValueAtTime(p * 1.3, t); bp.Frequency.ExponentialRampToValueAtTime(p * 0.7, t + d);
            n.Connect(bp); bp.Connect(g); g.Gain.SetValueAtTime(0.0001, t); g.Gain.LinearRampToValueAtTime(v, t + d * 0.3); g.Gain.ExponentialRampToValueAtTime(0.0001, t + d); Dest(g);
        }

        // js: vendor/ds/bundle.js:754
        public void Wood(double t, double p, double d, double v)
        {
            var o = Osc(OscType.Triangle, p, t, d); var g = ctx.CreateGain();
            o.Frequency.ExponentialRampToValueAtTime(p * 0.82, t + d);
            o.Connect(g); Env(g, t, 0.002, v, d); Dest(g);
            var n = NoiseSrc(t, 0.02); var hp = Filt(BiquadType.Highpass, 2500); var g2 = ctx.CreateGain(); n.Connect(hp); hp.Connect(g2); Env(g2, t, 0.001, v * 0.35, 0.018); Dest(g2);
        }

        // js: vendor/ds/bundle.js:760
        public void Thud(double t, double p, double d, double v)
        {
            var o = Osc(OscType.Sine, p, t, d); var g = ctx.CreateGain();
            o.Frequency.ExponentialRampToValueAtTime(p * 0.55, t + d);
            o.Connect(g); Env(g, t, 0.003, v, d); Dest(g);
        }

        // js: vendor/ds/bundle.js:765
        public void Swish(double t, double p, double d, double v)
        {
            var n = NoiseSrc(t, d); var bp = Filt(BiquadType.Bandpass, p * 0.5, 1.4); var g = ctx.CreateGain();
            bp.Frequency.SetValueAtTime(p * 0.45, t); bp.Frequency.ExponentialRampToValueAtTime(p * 1.7, t + d);
            n.Connect(bp); bp.Connect(g); g.Gain.SetValueAtTime(0.0001, t); g.Gain.ExponentialRampToValueAtTime(v, t + d * 0.6); g.Gain.ExponentialRampToValueAtTime(0.0001, t + d); Dest(g);
        }

        // js: vendor/ds/bundle.js:770
        public void Slash(double t, double p, double d, double v)
        {
            var n = NoiseSrc(t, d); var bp = Filt(BiquadType.Bandpass, p, 2.2); var g = ctx.CreateGain();
            bp.Frequency.SetValueAtTime(p, t); bp.Frequency.ExponentialRampToValueAtTime(p * 0.28, t + d * 0.8);
            n.Connect(bp); bp.Connect(g); Env(g, t, 0.003, v, d); Dest(g);
            Thud(t + 0.01, 120, 0.14, v * 0.7);
        }

        // js: vendor/ds/bundle.js:776
        public void Thrust(double t, double p, double d, double v)
        {
            var n = NoiseSrc(t, d); var hp = Filt(BiquadType.Bandpass, p, 3); var g = ctx.CreateGain();
            n.Connect(hp); hp.Connect(g); Env(g, t, 0.002, v * 0.8, d * 0.6); Dest(g);
            Thud(t, 170, d, v);
        }

        // js: vendor/ds/bundle.js:781
        public void Blunt(double t, double p, double d, double v)
        {
            Thud(t, p, d, v);
            var n = NoiseSrc(t, 0.08); var lp = Filt(BiquadType.Lowpass, 900); var g = ctx.CreateGain(); n.Connect(lp); lp.Connect(g); Env(g, t, 0.002, v * 0.6, 0.08); Dest(g);
        }

        private static readonly double[][] MetalPartials = { new[] { 1, 1, 1.0 }, new[] { 2.76, 0.45, 0.7 }, new[] { 5.4, 0.25, 0.45 }, new[] { 8.93, 0.12, 0.3 } };

        // js: vendor/ds/bundle.js:785
        public void Metal(double t, double p, double d, double v)
        {
            foreach (double[] r in MetalPartials)
            {
                var o = Osc(OscType.Sine, p * r[0], t, d); var g = ctx.CreateGain(); o.Connect(g); Env(g, t, 0.002, v * r[1] * (r[0] > 1 ? 0.6 + PTone() : 1), d * r[2]); Dest(g);
            }
            var n = NoiseSrc(t, 0.02); var hp = Filt(BiquadType.Highpass, 4000); var g2 = ctx.CreateGain(); n.Connect(hp); hp.Connect(g2); Env(g2, t, 0.001, v * 0.3, 0.02); Dest(g2);
        }

        // js: vendor/ds/bundle.js:791
        public void Crack(double t, double p, double d, double v)
        {
            Metal(t, p, d * 0.6, v * 0.7);
            var n = NoiseSrc(t, d); var hp = Filt(BiquadType.Highpass, 1800); var g = ctx.CreateGain(); n.Connect(hp); hp.Connect(g); Env(g, t + 0.02, 0.002, v * 0.8, d * 0.5); Dest(g);
        }

        // js: vendor/ds/bundle.js:795
        public void Seal(double t, double p, double d, double v)
        {
            Thud(t, p, d * 0.7, v);
            Wood(t + 0.012, 1500, 0.025, v * 0.5);
        }

        // js: vendor/ds/bundle.js:799
        public void String(double t, double p, double d, double v)
        {
            var lp = Filt(BiquadType.Lowpass, p * 10, 1.2); var g = ctx.CreateGain();
            lp.Frequency.SetValueAtTime(p * 10, t); lp.Frequency.ExponentialRampToValueAtTime(p * 1.6, t + d * 0.7);
            var o1 = Osc(OscType.Sawtooth, p, t, d); var o2 = Osc(OscType.Triangle, p * 1.003, t, d);
            o1.Connect(lp); o2.Connect(lp); lp.Connect(g); Env(g, t, 0.006, v, d); Dest(g);
        }

        // js: vendor/ds/bundle.js:805
        public void Chime(double t, double p, double d, double v)
        {
            var o = Osc(OscType.Sine, p, t, d); var o2 = Osc(OscType.Sine, p * 2.01, t, d * 0.5); var g = ctx.CreateGain(); var g2 = ctx.CreateGain();
            o.Connect(g); o2.Connect(g2); Env(g, t, 0.006, v, d); Env(g2, t, 0.004, v * 0.25, d * 0.5); Dest(g); Dest(g2);
        }

        // js: vendor/ds/bundle.js:809
        public void Breath(double t, double p, double d, double v)
        {
            var n = NoiseSrc(t, d); var bp = Filt(BiquadType.Bandpass, p, 0.7); var lp = Filt(BiquadType.Lowpass, 2200); var g = ctx.CreateGain();
            bp.Frequency.SetValueAtTime(p * 0.7, t); bp.Frequency.LinearRampToValueAtTime(p * 1.2, t + d);
            n.Connect(bp); bp.Connect(lp); lp.Connect(g);
            g.Gain.SetValueAtTime(0.0001, t); g.Gain.LinearRampToValueAtTime(v, t + d * 0.75); g.Gain.ExponentialRampToValueAtTime(0.0001, t + d); Dest(g);
        }

        // js: vendor/ds/bundle.js:815
        public void Body(double t, double p, double d, double v)
        {
            Thud(t, p, d, v);
            var n = NoiseSrc(t, d * 0.5); var lp = Filt(BiquadType.Lowpass, 420); var g = ctx.CreateGain(); n.Connect(lp); lp.Connect(g); Env(g, t, 0.003, v * 0.8, d * 0.5); Dest(g);
        }

        // js: vendor/ds/bundle.js:819
        public void Heart(double t, double p, double d, double v)
        {
            Thud(t, p, d, v); Thud(t + 0.16, p * 0.9, d, v * 0.7);
        }

        /// <summary>js: <c>SYN[timbre](t, p, d, v)</c>; false for a timbre the synth does not have.</summary>
        public bool Synth(string timbre, double t, double p, double d, double v)
        {
            switch (timbre)
            {
                case "paper": Paper(t, p, d, v); return true;
                case "slide": Slide(t, p, d, v); return true;
                case "wood": Wood(t, p, d, v); return true;
                case "thud": Thud(t, p, d, v); return true;
                case "swish": Swish(t, p, d, v); return true;
                case "slash": Slash(t, p, d, v); return true;
                case "thrust": Thrust(t, p, d, v); return true;
                case "blunt": Blunt(t, p, d, v); return true;
                case "metal": Metal(t, p, d, v); return true;
                case "crack": Crack(t, p, d, v); return true;
                case "seal": Seal(t, p, d, v); return true;
                case "string": String(t, p, d, v); return true;
                case "chime": Chime(t, p, d, v); return true;
                case "breath": Breath(t, p, d, v); return true;
                case "body": Body(t, p, d, v); return true;
                case "heart": Heart(t, p, d, v); return true;
                default: return false;
            }
        }

        /// <summary>
        /// js: vendor/ds/bundle.js:826 S.play(name, delayMs, mult): silent when muted, unknown or not unlocked; <c>hover</c> at most
        /// once per 50 ms of context time (as the JS writes it, a first hover at time 0 does not count); <c>turn++</c>; the
        /// ±3 % alternation for paper and wood; <c>vol × (mult || 1)</c> (a mult of 0 or NaN plays at 1). A bad preset is
        /// ignored. Returns whether a voice was built (the JS returns nothing).
        /// </summary>
        public bool Play(string name, double delayMs = 0, double? mult = null)
        {
            if (Muted) return false;
            if (name == null || !Presets.TryGetValue(name, out SoundPreset pr) || pr == null || !unlocked || !Ensure()) return false;
            double now = ctx.CurrentTime;
            if (name == "hover" && lastAt.TryGetValue("hover", out double lastHover) && lastHover != 0 && now - lastHover < SoundData.DsHoverThrottleS) return false;
            lastAt[name] = now; Turn++;
            curTone = pr.Tone;
            double t = now + (double.IsNaN(delayMs) ? 0 : delayMs) / 1000;
            // Alternate the pitch by ±3% so repeated paper sounds do not stack into one tone (no randomness).
            double wob = (pr.Timbre == "paper" || pr.Timbre == "wood") ? (Turn % 3 - 1) * 0.03 : 0;
            double m = mult.HasValue && mult.Value != 0 && !double.IsNaN(mult.Value) ? mult.Value : 1;
            try
            {
                return Synth(pr.Timbre, t, pr.Pitch * (1 + wob), pr.Dur / 1000, pr.Vol * m);
            }
            catch (Exception)
            {
                return false; // ignore a bad preset
            }
        }

        // js: vendor/ds/bundle.js:838
        public void Reset()
        {
            Presets = SoundData.DsSoundDefaults();
        }
    }
}
