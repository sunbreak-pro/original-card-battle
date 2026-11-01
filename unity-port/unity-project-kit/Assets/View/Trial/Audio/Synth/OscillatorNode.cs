// js: ctx.createOscillator(): sine from Math.Sin on a double phase, the other shapes from band-limited tables
// (WaveTables). Frequency and detune are a-rate; the phase starts at 0 on the start frame and advances by f/sr
// after each frame, as Chromium's virtual read index does.
using System;

namespace Trial.Audio.Synth
{
    public sealed class OscillatorNode : ScheduledSourceNode
    {
        public readonly AudioParam Frequency, Detune;
        private OscType type = OscType.Sine;
        private double phase;
        private WaveTables tables;
        private double lastF = double.NaN;
        private float[] higher, lower;
        private double blend;

        internal OscillatorNode(AudioContextLite context) : base(context, "Osc")
        {
            Frequency = NewParam("frequency", 440, -context.Nyquist, context.Nyquist);
            Detune = NewParam("detune", 0);
        }

        /// <summary>js: <c>osc.type = 'sine' | 'square' | 'sawtooth' | 'triangle'</c>.</summary>
        public OscType Type
        {
            get => type;
            set
            {
                type = value;
                tables = null;
                lastF = double.NaN;
                Context.Log?.Record(this, "type", TypeName(value));
            }
        }

        public static string TypeName(OscType t) =>
            t == OscType.Sine ? "sine" : t == OscType.Square ? "square" : t == OscType.Sawtooth ? "sawtooth" : "triangle";

        /// <summary>js: <c>osc.start(when)</c> (logged with offset 0, as the JS mock's default argument).</summary>
        public void Start(double when = 0) => StartAt(when, 0);

        protected override void Process(long startFrame, long quantum)
        {
            int n = AudioContextLite.RenderQuantumFrames;
            float[] o = Out[0];
            OutChannels = 1;
            Array.Clear(o, 0, n);
            long end = startFrame + n;
            if (!Started || StartFrame >= end || StopFrame <= startFrame || StopFrame <= StartFrame)
            {
                // Keep the timelines moving (their old events are dropped as time passes).
                Frequency.Fill(startFrame, quantum);
                return;
            }
            bool frequencyConstant = Frequency.Fill(startFrame, quantum);
            bool detuneConstant = Detune.Fill(startFrame, quantum);
            bool detuned = !detuneConstant || Detune.Values[0] != 0;
            int k0 = (int)Math.Max(0, StartFrame - startFrame);
            int k1 = (int)Math.Min(n, StopFrame - startFrame);
            double sr = Context.SampleRate, nyq = Context.Nyquist;
            float[] fv = Frequency.Values, dv = Detune.Values;
            if (type != OscType.Sine && tables == null) tables = WaveTables.For(type, Context.SampleRate);
            bool firstQuantum = StartFrame >= startFrame && StartFrame < end;
            // Chromium reads the quantum's frequency array from its first frame even when the oscillator starts later in the
            // quantum (OscillatorHandler::ProcessARate is handed phase_increments from index 0), so in its first quantum output
            // frame k uses the frequency of frame k − k0: the frames before the start, i.e. the value before setValueAtTime(f, t).
            int shift = firstQuantum && frequencyConstant && detuneConstant ? 0 : firstQuantum ? k0 : 0;
            // A start between two frames: with a constant (k-rate) frequency Chromium starts the phase on by the part of a frame
            // already gone; with an automated one it uses frequency 0 for that, i.e. starts at phase 0.
            if (firstQuantum && frequencyConstant && detuneConstant && StartFrameOffset < 0)
            {
                double f0 = fv[k0] * (detuned ? Math.Pow(2, dv[k0] / 1200) : 1);
                phase += -StartFrameOffset * f0 / sr;
                phase -= Math.Floor(phase);
            }
            for (int k = k0; k < k1; k++)
            {
                double f = fv[k - shift];
                if (detuned) f *= Math.Pow(2, dv[k - shift] / 1200);
                if (f > nyq) f = nyq; else if (f < -nyq) f = -nyq;
                if (type == OscType.Sine) o[k] = (float)Math.Sin(2 * Math.PI * phase);
                else
                {
                    if (f != lastF)
                    {
                        tables.Select(f, out higher, out lower, out blend);
                        lastF = f;
                    }
                    o[k] = tables.Read(phase, higher, lower, blend);
                }
                phase += f / sr;
                phase -= Math.Floor(phase);
            }
        }
    }
}
