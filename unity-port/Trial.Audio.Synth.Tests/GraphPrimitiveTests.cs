// The Web Audio subset, node by node: param timelines (spec semantics), the biquad (Q in dB for low / high pass), the
// oscillators and their band-limited tables, buffer sources, the equal-power panner, the convolver and its
// normalisation, the compressor's static curve, and pruning. Where Chromium does something particular (a source
// starting between two frames, an oscillator starting inside a render quantum), the expected numbers were read from
// headless Chromium 153's OfflineAudioContext.
using System;
using System.Linq;
using NUnit.Framework;
using Trial.Audio.Synth;

namespace Trial.Audio.Synth.Tests
{
    public class GraphPrimitiveTests
    {
        private const int Sr = 48000;

        /// <summary>A buffer of ones through a gain whose param the test automates, rendered frame by frame.</summary>
        private static float[] RenderGain(Action<AudioParam> automate, int frames, int sr = Sr)
        {
            var ctx = new AudioContextLite(sr);
            var ones = new AudioBuffer(1, 1024, sr);
            for (int i = 0; i < 1024; i++) ones.Channels[0][i] = 1;
            var src = ctx.CreateBufferSource(); src.Buffer = ones; src.Loop = true;
            var g = ctx.CreateGain();
            automate(g.Gain);
            src.Connect(g); g.Connect(ctx.Destination);
            src.Start(0);
            var l = new float[frames];
            var r = new float[frames];
            ctx.Render(l, r, 0, frames);
            return l;
        }

        // ---- params ------------------------------------------------------------------------------------------------

        [Test]
        public void SetValueAtTimeHoldsFromTheFirstFrameAtOrAfterItsTime()
        {
            // Chromium: gain 0, set 1 at frame 200.5 → frame 200 is 0, frame 201 is 1
            float[] o = RenderGain(p => { p.Value = 0; p.SetValueAtTime(1, 200.5 / Sr); }, 210);
            Assert.That(o[200], Is.EqualTo(0f));
            Assert.That(o[201], Is.EqualTo(1f));
            Assert.That(o[209], Is.EqualTo(1f));
        }

        [Test]
        public void LinearRampRunsFromThePreviousEvent()
        {
            // Chromium: set 0 at 0, linear to 1 at frame 100.5 → frame k is k/100.5, then 1
            float[] o = RenderGain(p => { p.SetValueAtTime(0, 0); p.LinearRampToValueAtTime(1, 100.5 / Sr); }, 110);
            Assert.That(o[96], Is.EqualTo(0.95522f).Within(1e-5));
            Assert.That(o[100], Is.EqualTo(0.99502f).Within(1e-5));
            Assert.That(o[101], Is.EqualTo(1f));
        }

        [Test]
        public void ExponentialRampIsGeometric()
        {
            float[] o = RenderGain(p => { p.SetValueAtTime(0.0001, 0); p.ExponentialRampToValueAtTime(1, 100.0 / Sr); }, 120);
            for (int k = 0; k <= 100; k += 10) Assert.That(o[k], Is.EqualTo(0.0001 * Math.Pow(10000, k / 100.0)).Within(1e-6 + 1e-5 * o[k]), "frame " + k);
            Assert.That(o[110], Is.EqualTo(1f));
        }

        [Test]
        public void ExponentialRampFromZeroHoldsTheStartUntilItsTime()
        {
            float[] o = RenderGain(p => { p.SetValueAtTime(0, 0); p.ExponentialRampToValueAtTime(0.5, 50.0 / Sr); }, 60);
            Assert.That(o[49], Is.EqualTo(0f));
            Assert.That(o[50], Is.EqualTo(0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => RenderGain(p => p.ExponentialRampToValueAtTime(0, 1), 1), "a ramp to 0 throws, as in browsers");
        }

        [Test]
        public void SetTargetApproachesFromTheValueAtItsStart()
        {
            double tau = 0.01;
            float[] o = RenderGain(p => { p.SetValueAtTime(0.2, 0); p.SetTargetAtTime(1, 100.0 / Sr, tau); }, 1200);
            Assert.That(o[99], Is.EqualTo(0.2f));
            foreach (int k in new[] { 100, 300, 600, 1100 })
                Assert.That(o[k], Is.EqualTo(1 + (0.2 - 1) * Math.Exp(-((k - 100.0) / Sr) / tau)).Within(1e-6), "frame " + k);
        }

        [Test]
        public void CancelThenSetTheCurrentValueFreezesARamp()
        {
            // as stopLayer does: cancel(now), setValueAtTime(cur, now), linearRamp(0, now + f)
            var ctx = new AudioContextLite(Sr);
            var g = ctx.CreateGain();
            g.Gain.SetValueAtTime(0, 0);
            g.Gain.LinearRampToValueAtTime(1, 1);
            ctx.RenderUntil(0.5);
            double now = ctx.CurrentTime; // the first quantum boundary at or after 0.5 s
            double cur = g.Gain.ValueAtTime(now);
            Assert.That(cur, Is.EqualTo(now).Within(1e-9), "halfway up the 1 s ramp");
            g.Gain.CancelScheduledValues(now);
            Assert.That(g.Gain.ValueAtTime(now + 0.1), Is.EqualTo(0), "the cancelled ramp no longer runs: the value falls back to the event before it");
            g.Gain.SetValueAtTime(cur, now);
            g.Gain.LinearRampToValueAtTime(0, now + 0.1);
            Assert.That(g.Gain.ValueAtTime(now + 0.05), Is.EqualTo(cur / 2).Within(1e-9));
        }

        [Test]
        public void ARampedVolumeChangedAgainContinuesFromWhereItGot()
        {
            // Audio.js:698 ramp(param, v) = cancel(now) + setTarget(v, now, 0.03), called again before the first settles
            // (a slider dragged): the second approach starts from the value the first reached, with no jump.
            var ctx = new AudioContextLite(Sr);
            var g = ctx.CreateGain();
            g.Gain.Value = 0.8;
            g.Gain.CancelScheduledValues(0); g.Gain.SetTargetAtTime(0.2, 0, 0.03);
            ctx.RenderUntil(0.04);
            double t1 = ctx.CurrentTime;
            double reached = 0.2 + (0.8 - 0.2) * Math.Exp(-t1 / 0.03);
            Assert.That(g.Gain.ValueAtTime(t1), Is.EqualTo(reached).Within(1e-9));
            g.Gain.CancelScheduledValues(t1); g.Gain.SetTargetAtTime(0.6, t1, 0.03);
            Assert.That(g.Gain.ValueAtTime(t1), Is.EqualTo(reached).Within(1e-9), "no jump at the second call");
            Assert.That(g.Gain.ValueAtTime(t1 + 0.03), Is.EqualTo(0.6 + (reached - 0.6) * Math.Exp(-1)).Within(1e-9));
        }

        [Test]
        public void ReadingValueGivesTheLastRenderedValue()
        {
            // stopLayer (Audio.js:674) reads p.value in the middle of the fade-in: Chromium gives the value at the end of the
            // last rendered quantum (the intrinsic value the render thread wrote back), not the ramp's target.
            var ctx = new AudioContextLite(Sr);
            var g = ctx.CreateGain();
            g.Connect(ctx.Destination);
            g.Gain.SetValueAtTime(0.0001, 0);
            g.Gain.LinearRampToValueAtTime(0.7, 1);
            ctx.RenderUntil(0.5);
            double last = (ctx.CurrentFrame - 1) / (double)Sr;
            Assert.That(g.Gain.Value, Is.EqualTo(0.0001 + (0.7 - 0.0001) * last).Within(1e-6));
            Assert.That(g.Gain.Value, Is.LessThan(0.7));
        }

        [Test]
        public void SameKindAtTheSameTimeReplaces()
        {
            var ctx = new AudioContextLite(Sr);
            var g = ctx.CreateGain();
            g.Gain.SetValueAtTime(0.3, 0.1);
            g.Gain.SetValueAtTime(0.7, 0.1);
            Assert.That(g.Gain.EventCount, Is.EqualTo(1), "the second set at 0.1 replaced the first");
            Assert.That(g.Gain.ValueAtTime(0.2), Is.EqualTo(0.7).Within(1e-9));
        }

        [Test]
        public void ValueSetterIsASetValueAtTheCurrentTime()
        {
            var ctx = new AudioContextLite(Sr);
            var g = ctx.CreateGain();
            g.Gain.SetValueAtTime(0.25, 0);
            ctx.RenderUntil(0.1);
            g.Gain.Value = 0.9;
            Assert.That(g.Gain.Value, Is.EqualTo(0.9));
            Assert.That(g.Gain.ValueAtTime(ctx.CurrentTime), Is.EqualTo(0.9).Within(1e-7));
        }

        [Test]
        public void ParamInputsAddToTheValue()
        {
            var ctx = new AudioContextLite(Sr);
            var ones = new AudioBuffer(1, 128, Sr);
            for (int i = 0; i < 128; i++) ones.Channels[0][i] = 1;
            var src = ctx.CreateBufferSource(); src.Buffer = ones; src.Loop = true;
            var lfo = ctx.CreateOscillator(); lfo.Frequency.Value = 375; // 128 frames per cycle
            var depth = ctx.CreateGain(); depth.Gain.Value = 0.4;
            var am = ctx.CreateGain(); am.Gain.Value = 0.6;
            lfo.Connect(depth); depth.Connect(am.Gain);
            src.Connect(am); am.Connect(ctx.Destination);
            src.Start(0); lfo.Start(0);
            var l = new float[256];
            var r = new float[256];
            ctx.Render(l, r, 0, 256);
            for (int k = 0; k < 256; k += 16) Assert.That(l[k], Is.EqualTo(0.6 + 0.4 * Math.Sin(2 * Math.PI * 375 * k / Sr)).Within(1e-5), "frame " + k);
        }

        // ---- biquad ------------------------------------------------------------------------------------------------

        [TestCase(BiquadType.Lowpass, 2400, 1.0)]
        [TestCase(BiquadType.Lowpass, 650, 1.2)]
        [TestCase(BiquadType.Highpass, 2800, 0.7)]
        [TestCase(BiquadType.Highpass, 500, 0.0)]
        public void LowAndHighPassTakeQInDecibels(BiquadType type, double f, double qDb)
        {
            BiquadCoefficients c = BiquadCoefficients.Of(type, f, qDb, 0, Sr);
            Assert.That(20 * Math.Log10(c.MagnitudeAt(f, Sr)), Is.EqualTo(qDb).Within(0.01), "the gain at the cutoff is Q dB");
            double far = type == BiquadType.Lowpass ? 20 : 20000;
            Assert.That(c.MagnitudeAt(far, Sr), Is.EqualTo(1).Within(0.01), "unity in the pass band");
        }

        [TestCase(3400, 1.8)]
        [TestCase(620, 14)]
        [TestCase(900, 0.8)]
        public void BandPassPeaksAtUnityWithLinearQ(double f, double q)
        {
            BiquadCoefficients c = BiquadCoefficients.Of(BiquadType.Bandpass, f, q, 0, Sr);
            Assert.That(c.MagnitudeAt(f, Sr), Is.EqualTo(1).Within(1e-6));
            // the −3 dB points are f0·2^(±BW/2) with Q = 1/(2 sinh(ln2/2·BW·w0/sin w0)) — check the bandwidth roughly
            Assert.That(c.MagnitudeAt(f * 4, Sr), Is.LessThan(1));
        }

        [Test]
        public void FilterNodeMatchesItsResponseOnASine()
        {
            var ctx = new AudioContextLite(Sr);
            var o = ctx.CreateOscillator(); o.Frequency.Value = 1000;
            var b = ctx.CreateBiquadFilter(); b.Type = BiquadType.Lowpass; b.Frequency.Value = 1000; b.Q.Value = 6;
            o.Connect(b); b.Connect(ctx.Destination); o.Start(0);
            var l = new float[48000];
            var r = new float[48000];
            ctx.Render(l, r, 0, 48000);
            float peak = l.Skip(24000).Max(Math.Abs);
            Assert.That(20 * Math.Log10(peak), Is.EqualTo(6).Within(0.05), "a 1 kHz sine at the cutoff of a Q 6 dB low-pass comes out 6 dB up");
            Assert.That(b.Type, Is.EqualTo(BiquadType.Lowpass));
            Assert.That(b.Q.DefaultValue, Is.EqualTo(1), "the default Q the design system's wet low-pass relies on");
        }

        // ---- oscillators -------------------------------------------------------------------------------------------

        [Test]
        public void SineStartsAtPhaseZeroOnAQuantumBoundary()
        {
            var ctx = new AudioContextLite(Sr);
            var o = ctx.CreateOscillator(); o.Frequency.SetValueAtTime(1000, 128.0 / Sr);
            o.Connect(ctx.Destination); o.Start(128.0 / Sr); o.Stop(1);
            var l = new float[512];
            var r = new float[512];
            ctx.Render(l, r, 0, 512);
            Assert.That(l[127], Is.EqualTo(0f));
            for (int k = 128; k < 512; k += 7) Assert.That(l[k], Is.EqualTo(Math.Sin(2 * Math.PI * 1000 * (k - 128) / Sr)).Within(1e-6));
        }

        /// <summary>Chromium 153: an oscillator started inside a quantum reads the frequency of the quantum's first frames.</summary>
        [Test]
        public void OscillatorStartedInsideAQuantumRunsItsFirstFramesAtTheEarlierFrequency()
        {
            float[] Run(double startFrame, bool automated)
            {
                var ctx = new AudioContextLite(Sr);
                var o = ctx.CreateOscillator();
                double t = startFrame / Sr;
                if (automated) o.Frequency.SetValueAtTime(1000, t); else o.Frequency.Value = 1000;
                o.Connect(ctx.Destination); o.Start(t); o.Stop(t + 0.005);
                var l = new float[512];
                var r = new float[512];
                ctx.Render(l, r, 0, 512);
                return l.Skip(196).Take(16).ToArray();
            }
            // values read from Chromium (slice 196..211)
            float[] auto200 = { 0, 0, 0, 0, 0, 0.05756f, 0.11494f, 0.17193f, 0.22835f, 0.28402f, 0.33874f, 0.39234f, 0.44464f, 0.49546f, 0.54464f, 0.59201f };
            float[] auto2008 = { 0, 0, 0, 0, 0, 0, 0.05756f, 0.11494f, 0.17193f, 0.22835f, 0.28402f, 0.33874f, 0.39234f, 0.44464f, 0.49546f, 0.54464f };
            float[] k2008 = { 0, 0, 0, 0, 0, 0.02618f, 0.15643f, 0.28402f, 0.40674f, 0.5225f, 0.62932f, 0.72537f, 0.80902f, 0.87882f, 0.93358f, 0.97237f };
            float[] k200 = { 0, 0, 0, 0, 0, 0.13053f, 0.25882f, 0.38268f, 0.5f, 0.60876f, 0.70711f, 0.79335f, 0.86603f, 0.92388f, 0.96593f, 0.99144f };
            Assert.That(Run(200, true), Is.EqualTo(auto200).Within(2e-5), "automated, start on frame 200 (440 Hz first)");
            Assert.That(Run(200.8, true), Is.EqualTo(auto2008).Within(2e-5), "automated, start at 200.8: phase 0 on frame 201");
            Assert.That(Run(200.8, false), Is.EqualTo(k2008).Within(2e-5), "constant, start at 200.8: phase on by 0.2 frame");
            Assert.That(Run(200, false), Is.EqualTo(k200).Within(2e-5), "constant, start on frame 200");
        }

        [TestCase(OscType.Sawtooth)]
        [TestCase(OscType.Triangle)]
        [TestCase(OscType.Square)]
        public void BandLimitedTablesHaveNoPartialAboveNyquist(OscType type)
        {
            WaveTables w = WaveTables.For(type, Sr);
            Assert.That(w.TableSize, Is.EqualTo(4096));
            Assert.That(w.NumberOfRanges, Is.EqualTo(36));
            Assert.That(w.LowestFundamental, Is.EqualTo(24000.0 / 2048));
            Assert.That(w.Tables[0].Max(Math.Abs), Is.EqualTo(1f).Within(1e-6), "range 0 is scaled to peak 1");
            foreach (double f in new[] { 41.2, 98, 300, 1500, 5000 })
            {
                w.Select(f, out float[] higher, out float[] lower, out double blend);
                foreach (float[] table in new[] { higher, lower })
                {
                    var re = table.Select(x => (double)x).ToArray();
                    var im = new double[re.Length];
                    Fft.Of(re.Length).Forward(re, im);
                    int maxPartial = (int)Math.Floor(24000 / f);
                    double alias = 0, total = 0;
                    for (int n = 1; n < re.Length / 2; n++)
                    {
                        double p = re[n] * re[n] + im[n] * im[n];
                        total += p;
                        if (n > maxPartial) alias += p;
                    }
                    // what is left above the limit is the Float32 rounding of the table (about −150 dB)
                    Assert.That(alias / total, Is.LessThan(1e-12), $"{type} at {f} Hz: no partial above Nyquist");
                }
            }
        }

        [Test]
        public void SawtoothRisesFromZeroAndFallsAtHalfPeriod()
        {
            var ctx = new AudioContextLite(Sr);
            var o = ctx.CreateOscillator(); o.Type = OscType.Sawtooth; o.Frequency.Value = 375; // 128 frames per period
            o.Connect(ctx.Destination); o.Start(0);
            var l = new float[512];
            var r = new float[512];
            ctx.Render(l, r, 0, 512);
            Assert.That(l[128 + 32], Is.GreaterThan(0.4f), "rising in the first half");
            Assert.That(l[128 + 96], Is.LessThan(-0.4f), "negative in the second half");
            Assert.That(l.Max(Math.Abs), Is.LessThanOrEqualTo(1.0001f));
        }

        // ---- buffer sources ----------------------------------------------------------------------------------------

        /// <summary>Chromium 153: the offset rounds to a frame; a start between frames reads on by the part already gone.</summary>
        [TestCase(200.2, 0.0, new float[] { 0, 0, 0, 0, 0, 0.8f, 1.8f, 2.8f, 3.8f, 4.8f, 5.8f, 6.8f, 7.8f, 8.8f, 9.8f, 10.8f })]
        [TestCase(200.8, 0.0, new float[] { 0, 0, 0, 0, 0, 0.2f, 1.2f, 2.2f, 3.2f, 4.2f, 5.2f, 6.2f, 7.2f, 8.2f, 9.2f, 10.2f })]
        [TestCase(200.0, 0.0, new float[] { 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 })]
        [TestCase(200.0, 10.5, new float[] { 0, 0, 0, 0, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 })]
        [TestCase(200.8, 10.0, new float[] { 0, 0, 0, 0, 0, 10.2f, 11.2f, 12.2f, 13.2f, 14.2f, 15.2f, 16.2f, 17.2f, 18.2f, 19.2f, 20.2f })]
        public void BufferSourceStartsLikeChromium(double startFrame, double offsetFrames, float[] expected)
        {
            var ctx = new AudioContextLite(Sr);
            var ramp = new AudioBuffer(1, 1000, Sr);
            for (int i = 0; i < 1000; i++) ramp.Channels[0][i] = i;
            var s = ctx.CreateBufferSource(); s.Buffer = ramp; s.Loop = true;
            s.Connect(ctx.Destination);
            double t = startFrame / Sr;
            s.Start(t, offsetFrames / Sr); s.Stop(t + 0.005);
            var l = new float[512];
            var r = new float[512];
            ctx.Render(l, r, 0, 512);
            Assert.That(l.Skip(196).Take(16).ToArray(), Is.EqualTo(expected).Within(1e-4));
        }

        [Test]
        public void LoopingSourceWrapsAndStops()
        {
            var ctx = new AudioContextLite(Sr);
            var b = new AudioBuffer(1, 100, Sr);
            for (int i = 0; i < 100; i++) b.Channels[0][i] = i;
            var s = ctx.CreateBufferSource(); s.Buffer = b; s.Loop = true;
            s.Connect(ctx.Destination); s.Start(0, 90.0 / Sr); s.Stop(300.0 / Sr);
            var l = new float[400];
            var r = new float[400];
            ctx.Render(l, r, 0, 400);
            Assert.That(l[0], Is.EqualTo(90f));
            Assert.That(l[9], Is.EqualTo(99f));
            Assert.That(l[10], Is.EqualTo(0f), "wraps to the start");
            Assert.That(l[11], Is.EqualTo(1f));
            Assert.That(l[290], Is.EqualTo(80f));
            long stopFrame = ctx.FrameAtOrAfter(300.0 / Sr);
            Assert.That(l[stopFrame - 1], Is.Not.EqualTo(0f));
            Assert.That(l.Skip((int)stopFrame).All(x => x == 0), Is.True, "silent from its stop frame");
        }

        // ---- panner ------------------------------------------------------------------------------------------------

        [TestCase(0.12, 0.6374, 0.7705)]
        [TestCase(-0.12, 0.7705, 0.6374)]
        [TestCase(0.0, 0.70711, 0.70711)]
        public void StereoPannerIsEqualPower(double pan, double gl, double gr)
        {
            var ctx = new AudioContextLite(Sr);
            var ones = new AudioBuffer(1, 128, Sr);
            for (int i = 0; i < 128; i++) ones.Channels[0][i] = 1;
            var s = ctx.CreateBufferSource(); s.Buffer = ones; s.Loop = true;
            var p = ctx.CreateStereoPanner(); p.Pan.Value = pan;
            s.Connect(p); p.Connect(ctx.Destination); s.Start(0);
            var l = new float[128];
            var r = new float[128];
            ctx.Render(l, r, 0, 128);
            Assert.That(l[64], Is.EqualTo(gl).Within(1e-4));
            Assert.That(r[64], Is.EqualTo(gr).Within(1e-4));
        }

        [Test]
        public void MonoIntoTheDestinationIsUpMixedWithoutLoss()
        {
            var ctx = new AudioContextLite(Sr);
            var ones = new AudioBuffer(1, 128, Sr);
            for (int i = 0; i < 128; i++) ones.Channels[0][i] = 0.5f;
            var s = ctx.CreateBufferSource(); s.Buffer = ones; s.Loop = true;
            s.Connect(ctx.Destination); s.Start(0);
            var l = new float[128];
            var r = new float[128];
            ctx.Render(l, r, 0, 128);
            Assert.That(l[10], Is.EqualTo(0.5f));
            Assert.That(r[10], Is.EqualTo(0.5f));
        }

        // ---- convolver ---------------------------------------------------------------------------------------------

        [TestCase(128)]
        [TestCase(4096)]
        public void UnitImpulseThroughTheConvolverReturnsTheScaledImpulse(int block)
        {
            var ctx = new AudioContextLite(Sr);
            AudioBuffer ir = AudioBuffers.MakeDampRoom(Sr);
            double scale = AudioBuffers.ConvolverNormalizationScale(ir);
            var impulse = new AudioBuffer(1, 1, Sr);
            impulse.Channels[0][0] = 1;
            var s = ctx.CreateBufferSource(); s.Buffer = impulse;
            var conv = ctx.CreateConvolver(block); conv.Buffer = ir;
            s.Connect(conv); conv.Connect(ctx.Destination); s.Start(0);
            int frames = ir.Length + block + 256;
            var l = new float[frames];
            var r = new float[frames];
            ctx.Render(l, r, 0, frames);
            int lat = conv.LatencyFrames;
            Assert.That(lat, Is.EqualTo(block == 128 ? 0 : block));
            for (int i = 0; i < ir.Length; i += 997)
            {
                Assert.That(l[lat + i], Is.EqualTo(ir.Channels[0][i] * scale).Within(1e-7), "L " + i);
                Assert.That(r[lat + i], Is.EqualTo(ir.Channels[1][i] * scale).Within(1e-7), "R " + i);
            }
            Assert.That(Math.Abs(l[lat + ir.Length + 10]), Is.LessThan(1e-7f), "nothing after the impulse");
        }

        [Test]
        public void OfflineConvolutionEqualsTheNode()
        {
            var rnd = new Lcg(5);
            var inL = new float[20000];
            var inR = new float[20000];
            for (int i = 0; i < 5000; i++) { inL[i] = (float)(rnd.Next() * 2 - 1); inR[i] = (float)(rnd.Next() * 2 - 1); }
            AudioBuffer ir = AudioBuffers.MakeDampRoom(Sr);
            var outL = new float[20000];
            var outR = new float[20000];
            PartitionedConvolver.ConvolveOffline(inL, inR, ir.Channels, 0.5, 20000, outL, outR, 2048);
            // direct convolution at a few frames
            foreach (int n in new[] { 0, 17, 4999, 12345, 19999 })
            {
                double yl = 0, yr = 0;
                for (int k = 0; k <= n && k < 5000; k++)
                {
                    if (n - k < ir.Length) { yl += inL[k] * (double)ir.Channels[0][n - k]; yr += inR[k] * (double)ir.Channels[1][n - k]; }
                }
                Assert.That(outL[n], Is.EqualTo(0.5 * yl).Within(1e-4), "L " + n);
                Assert.That(outR[n], Is.EqualTo(0.5 * yr).Within(1e-4), "R " + n);
            }
        }

        // ---- compressor --------------------------------------------------------------------------------------------

        [Test]
        public void CompressorStaticCurveAndMakeup()
        {
            DynamicsCompressor c = DynamicsCompressor.Trial(Sr);
            float makeup = c.StaticGain(0.01f);
            Assert.That(makeup, Is.EqualTo(1.1351f).Within(0.002f), "below the threshold only the makeup (1/curve(1))^0.6 acts");
            Assert.That(c.StaticGain(1f), Is.EqualTo(0.8096f * 1.1351f).Within(0.003f), "at 0 dBFS: curve(1) = 0.8096 inside the knee, times the makeup");
            // a steady loud sine settles to the static gain
            var buf = new float[2 * Sr * 2];
            for (int i = 0; i < Sr * 2; i++) buf[2 * i] = buf[2 * i + 1] = (float)Math.Sin(2 * Math.PI * 100 * i / Sr);
            c.WarmUp(0.5);
            c.Process(buf, Sr * 2, 2);
            float peak = 0;
            for (int i = Sr; i < Sr * 2; i++) peak = Math.Max(peak, Math.Abs(buf[2 * i]));
            Assert.That(20 * Math.Log10(peak), Is.EqualTo(20 * Math.Log10(c.StaticGain(1f))).Within(1.0), "within 1 dB of the static gain");
        }

        [Test]
        public void CompressorDelaysBySixMilliseconds()
        {
            DynamicsCompressor c = DynamicsCompressor.Trial(Sr);
            c.WarmUp(0.5);
            var buf = new float[2 * 1000];
            buf[0] = buf[1] = 0.01f;
            c.Process(buf, 1000, 2);
            int at = Enumerable.Range(0, 1000).First(i => buf[2 * i] != 0);
            Assert.That(at, Is.EqualTo(288));
            Assert.That(buf[2 * at], Is.EqualTo(0.01f * 1.1351f).Within(1e-4f));
        }

        // ---- pruning -----------------------------------------------------------------------------------------------

        [Test]
        public void FinishedTransientVoicesLeaveTheGraph()
        {
            var ctx = new AudioContextLite(Sr);
            var bus = ctx.CreateGain(); bus.Connect(ctx.Destination);
            ctx.TransientNodes = true;
            var synth = new MetaSynth(ctx, bus, ctx.CreateGain(), Golden.Bank);
            synth.PlayPreset(SoundData.DefaultPreset("journalOpen"), 0);
            Assert.That(bus.InputCount, Is.GreaterThan(0));
            ctx.RenderUntil(1.5);
            Assert.That(bus.InputCount, Is.EqualTo(0), "the voice's nodes were dropped after its stop and the filters' tail");
            Assert.That(ctx.Destination.InputCount, Is.EqualTo(1), "the bus is not transient and stays");
        }
    }
}
