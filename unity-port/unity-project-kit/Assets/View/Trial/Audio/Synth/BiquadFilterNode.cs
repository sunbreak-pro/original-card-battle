// js: ctx.createBiquadFilter(): the Web Audio biquad (Audio EQ Cookbook, as Chromium's Biquad computes it).
// The frequency is normalised by Nyquist and clamped to [0, 1]; lowpass / highpass take Q in dB
// (α = sin w0 / (2·10^(Q/20))), bandpass takes Q linear (α = sin w0 / (2Q)). The coefficients are computed per sample
// while frequency / Q / detune are automated or modulated, once otherwise. Direct form 1 with double state per channel.
// The default Q is 1, which the design system's wet low-pass relies on.
using System;

namespace Trial.Audio.Synth
{
    public enum BiquadType
    {
        Lowpass,
        Highpass,
        Bandpass,
    }

    public struct BiquadCoefficients
    {
        public double B0, B1, B2, A1, A2;

        /// <summary>The coefficients Chromium's Biquad sets for a type, frequency (Hz), Q and detune (cents), normalised by a0.</summary>
        public static BiquadCoefficients Of(BiquadType type, double frequency, double q, double detune, double sampleRate)
        {
            double nyquist = sampleRate / 2;
            double f = frequency / nyquist;
            if (detune != 0) f *= Math.Pow(2, detune / 1200);
            switch (type)
            {
                case BiquadType.Lowpass: return Lowpass(f, q);
                case BiquadType.Highpass: return Highpass(f, q);
                default: return Bandpass(f, q);
            }
        }

        private static BiquadCoefficients Normalized(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            double k = 1 / a0;
            return new BiquadCoefficients { B0 = b0 * k, B1 = b1 * k, B2 = b2 * k, A1 = a1 * k, A2 = a2 * k };
        }

        private static BiquadCoefficients Lowpass(double cutoff, double resonance)
        {
            cutoff = cutoff < 0 ? 0 : cutoff > 1 ? 1 : cutoff;
            if (cutoff == 1) return Normalized(1, 0, 0, 1, 0, 0);
            if (cutoff > 0)
            {
                resonance = Math.Pow(10, resonance / 20);
                double theta = Math.PI * cutoff;
                double alpha = Math.Sin(theta) / (2 * resonance);
                double cosw = Math.Cos(theta);
                double beta = (1 - cosw) / 2;
                return Normalized(beta, 2 * beta, beta, 1 + alpha, -2 * cosw, 1 - alpha);
            }
            return Normalized(0, 0, 0, 1, 0, 0);
        }

        private static BiquadCoefficients Highpass(double cutoff, double resonance)
        {
            cutoff = cutoff < 0 ? 0 : cutoff > 1 ? 1 : cutoff;
            if (cutoff == 1) return Normalized(0, 0, 0, 1, 0, 0);
            if (cutoff > 0)
            {
                resonance = Math.Pow(10, resonance / 20);
                double theta = Math.PI * cutoff;
                double alpha = Math.Sin(theta) / (2 * resonance);
                double cosw = Math.Cos(theta);
                double beta = (1 + cosw) / 2;
                return Normalized(beta, -2 * beta, beta, 1 + alpha, -2 * cosw, 1 - alpha);
            }
            return Normalized(1, 0, 0, 1, 0, 0);
        }

        private static BiquadCoefficients Bandpass(double frequency, double q)
        {
            frequency = Math.Max(0, frequency);
            q = Math.Max(0, q);
            if (frequency > 0 && frequency < 1)
            {
                double w0 = Math.PI * frequency;
                if (q > 0)
                {
                    double alpha = Math.Sin(w0) / (2 * q);
                    double k = Math.Cos(w0);
                    return Normalized(alpha, 0, -alpha, 1 + alpha, -2 * k, 1 - alpha);
                }
                return Normalized(1, 0, 0, 1, 0, 0);
            }
            return Normalized(0, 0, 0, 1, 0, 0);
        }

        /// <summary>|H(e^{jw})| at <paramref name="hz"/> (tests).</summary>
        public double MagnitudeAt(double hz, double sampleRate)
        {
            double w = 2 * Math.PI * hz / sampleRate;
            double c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2 * w), s2 = Math.Sin(2 * w);
            double nr = B0 + B1 * c1 + B2 * c2, ni = -(B1 * s1 + B2 * s2);
            double dr = 1 + A1 * c1 + A2 * c2, di = -(A1 * s1 + A2 * s2);
            return Math.Sqrt((nr * nr + ni * ni) / (dr * dr + di * di));
        }
    }

    /// <summary>The per-channel state of a biquad (direct form 1, as Chromium's Biquad::Process).</summary>
    public sealed class BiquadState
    {
        public double X1, X2, Y1, Y2;

        public void Reset()
        {
            X1 = X2 = Y1 = Y2 = 0;
        }

        /// <summary>Filters <paramref name="count"/> samples in place with fixed coefficients.</summary>
        public void Process(float[] data, int offset, int count, in BiquadCoefficients c)
        {
            double x1 = X1, x2 = X2, y1 = Y1, y2 = Y2;
            double b0 = c.B0, b1 = c.B1, b2 = c.B2, a1 = c.A1, a2 = c.A2;
            for (int i = offset, end = offset + count; i < end; i++)
            {
                double x = data[i];
                double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                data[i] = (float)y;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
            }
            X1 = x1; X2 = x2; Y1 = y1; Y2 = y2;
            FlushSubnormals(data, offset, count);
        }

        internal void FlushSubnormals(float[] data, int offset, int count)
        {
            // Chromium: avoid a stream of subnormals when the input is silent and the tail approaches zero.
            const double FltMin = 1.17549435e-38;
            if (X1 == 0 && X2 == 0 && (Y1 != 0 || Y2 != 0) && Math.Abs(Y1) < FltMin && Math.Abs(Y2) < FltMin)
            {
                Y1 = Y2 = 0;
                for (int i = offset + count; i-- > offset && Math.Abs(data[i]) < FltMin;) data[i] = 0;
            }
        }
    }

    public sealed class BiquadFilterNode : AudioNode
    {
        public readonly AudioParam Frequency, Q, Gain, Detune;
        private BiquadType type = BiquadType.Lowpass;
        private readonly BiquadState[] state = { new BiquadState(), new BiquadState() };
        private BiquadCoefficients cached;
        private bool hasCached;
        private float cachedF, cachedQ, cachedDetune;
        private BiquadType cachedType;

        internal BiquadFilterNode(AudioContextLite context) : base(context, "Biquad")
        {
            Frequency = NewParam("frequency", 350, 0, context.Nyquist);
            Q = NewParam("Q", 1);
            Gain = NewParam("gain", 0);
            Detune = NewParam("detune", 0);
        }

        /// <summary>js: <c>filter.type = 'lowpass' | 'highpass' | 'bandpass'</c>.</summary>
        public BiquadType Type
        {
            get => type;
            set
            {
                type = value;
                Context.Log?.Record(this, "type", TypeName(value));
            }
        }

        public static string TypeName(BiquadType t) => t == BiquadType.Lowpass ? "lowpass" : t == BiquadType.Highpass ? "highpass" : "bandpass";

        /// <summary>A filter rings for a while after its input stops (a generous bound for the trial's Q values).</summary>
        protected override long TailFrames => Context.SampleRate / 4;

        protected override void Process(long startFrame, long quantum)
        {
            int n = AudioContextLite.RenderQuantumFrames;
            OutChannels = MixInputs(Out);
            bool fc = Frequency.Fill(startFrame, quantum);
            bool qc = Q.Fill(startFrame, quantum);
            bool dc = Detune.Fill(startFrame, quantum);
            double sr = Context.SampleRate;
            if (fc && qc && dc)
            {
                float f = Frequency.Values[0], q = Q.Values[0], de = Detune.Values[0];
                if (!hasCached || f != cachedF || q != cachedQ || de != cachedDetune || type != cachedType)
                {
                    cached = BiquadCoefficients.Of(type, f, q, de, sr);
                    cachedF = f; cachedQ = q; cachedDetune = de; cachedType = type;
                    hasCached = true;
                }
                for (int c = 0; c < OutChannels; c++) state[c].Process(Out[c], 0, n, cached);
                return;
            }
            // Sample-accurate coefficients while the params move.
            hasCached = false;
            float[] fv = Frequency.Values, qv = Q.Values, dv = Detune.Values;
            for (int c = 0; c < OutChannels; c++)
            {
                BiquadState s = state[c];
                float[] d = Out[c];
                double x1 = s.X1, x2 = s.X2, y1 = s.Y1, y2 = s.Y2;
                for (int k = 0; k < n; k++)
                {
                    BiquadCoefficients co = BiquadCoefficients.Of(type, fv[k], qv[k], dv[k], sr);
                    double x = d[k];
                    double y = co.B0 * x + co.B1 * x1 + co.B2 * x2 - co.A1 * y1 - co.A2 * y2;
                    d[k] = (float)y;
                    x2 = x1; x1 = x; y2 = y1; y1 = y;
                }
                s.X1 = x1; s.X2 = x2; s.Y1 = y1; s.Y2 = y2;
                s.FlushSubnormals(d, 0, n);
            }
        }
    }
}
