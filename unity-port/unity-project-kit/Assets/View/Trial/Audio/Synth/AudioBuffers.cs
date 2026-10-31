// Audio buffers and the procedural ones the trial's sound builds: the meta noises (white / pink / brown, 4 s seamless
// loops), the design system's 1 s white noise, the decaying-noise room impulses, and the ConvolverNode normalisation.
// Samples are stored as Float32 exactly as the JS Float32Array stores them (the arithmetic before the store is double).
using System;

namespace Trial.Audio.Synth
{
    public enum NoiseColor
    {
        White,
        Pink,
        Brown,
    }

    /// <summary>A WebAudio AudioBuffer: planar Float32 channels at a sample rate. <see cref="Name"/> labels it in call logs.</summary>
    public sealed class AudioBuffer
    {
        public readonly float[][] Channels;
        public int Length { get; }
        public int SampleRate { get; }
        public double Duration => (double)Length / SampleRate;
        public int NumberOfChannels => Channels.Length;
        public string Name { get; set; }

        public AudioBuffer(int numberOfChannels, int length, int sampleRate)
        {
            if (numberOfChannels < 1 || length <= 0 || sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(length), "bad buffer");
            Length = length;
            SampleRate = sampleRate;
            Channels = new float[numberOfChannels][];
            for (int c = 0; c < numberOfChannels; c++) Channels[c] = new float[length];
        }

        public float[] GetChannelData(int channel) => Channels[channel];
    }

    /// <summary>The three meta noise buffers, made once in <c>ensure()</c> (Audio.js:302), in that order.</summary>
    public sealed class NoiseBank
    {
        public readonly AudioBuffer White, Pink, Brown;

        // js: scenes/audio/Audio.js:302
        public NoiseBank(int sampleRate)
        {
            White = AudioBuffers.MakeNoise(sampleRate, 11, NoiseColor.White);
            Pink = AudioBuffers.MakeNoise(sampleRate, 23, NoiseColor.Pink);
            Brown = AudioBuffers.MakeNoise(sampleRate, 37, NoiseColor.Brown);
        }

        /// <summary>js: <c>bufs[color || 'white']</c>.</summary>
        public AudioBuffer Get(NoiseColor color) => color == NoiseColor.Pink ? Pink : color == NoiseColor.Brown ? Brown : White;
    }

    public static class AudioBuffers
    {
        /// <summary>js: scenes/audio/Audio.js:121 NOISE_SECONDS.</summary>
        public const double NoiseSeconds = 4;

        /// <summary>
        /// js: scenes/audio/Audio.js:242 makeNoise(seed, color): <c>floor(sr·4)</c> frames of white, pink (Paul Kellet's refined
        /// filter) or brown noise from <c>lcg(seed)</c>, the tail cross-faded into the head over 0.25 s (a seamless loop) and
        /// scaled to peak at 0.9 (the peak taken on the double values before the Float32 store).
        /// </summary>
        public static AudioBuffer MakeNoise(int sampleRate, int seed, NoiseColor color)
        {
            int sr = sampleRate, n = (int)Math.Floor(sr * NoiseSeconds), fade = (int)Math.Floor(sr * 0.25);
            var raw = new float[n + fade];
            var rnd = new Lcg(seed);
            double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0, last = 0, peak = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                double w = rnd.Next() * 2 - 1;
                double x = w;
                if (color == NoiseColor.Pink)
                {
                    // Paul Kellet's refined filter
                    b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.969 * b2 + w * 0.153852;
                    b3 = 0.8665 * b3 + w * 0.3104856; b4 = 0.55 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.016898;
                    x = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362; b6 = w * 0.115926;
                }
                else if (color == NoiseColor.Brown)
                {
                    last = (last + 0.02 * w) / 1.02; x = last;
                }
                raw[i] = (float)x;
                if (Math.Abs(x) > peak) peak = Math.Abs(x);
            }
            var buf = new AudioBuffer(1, n, sr) { Name = color == NoiseColor.Pink ? "pink" : color == NoiseColor.Brown ? "brown" : "white" };
            float[] d = buf.Channels[0];
            double k = peak > 0 ? 0.9 / peak : 1;
            // Crossfade the tail into the head so the loop has no seam (brown noise would click otherwise).
            for (int i = 0; i < n; i++)
            {
                double v = i < fade ? raw[i] * ((double)i / fade) + raw[n + i] * (1 - (double)i / fade) : raw[i];
                d[i] = (float)(v * k);
            }
            return buf;
        }

        /// <summary>
        /// js: vendor/ds/bundle.js:726 the design system's noise: one mono second, seed 11,
        /// <c>sd = (sd·16807) % 2147483647; nd[j] = (sd / 2147483647)·2 − 1</c> (no normalisation, no loop fade).
        /// </summary>
        public static AudioBuffer MakeDsNoise(int sampleRate)
        {
            var nb = new AudioBuffer(1, sampleRate, sampleRate) { Name = "dsNoise" };
            float[] nd = nb.Channels[0];
            long sd = 11;
            for (int j = 0; j < nd.Length; j++)
            {
                sd = (sd * 16807) % 2147483647;
                nd[j] = (float)((sd / 2147483647d) * 2 - 1);
            }
            return nb;
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:267 makeRoom(seconds, power, seed) / vendor/ds/bundle.js:716: a stereo impulse of
        /// <c>floor(sr·seconds)</c> frames of decaying noise, <c>((s / 2147483647)·2 − 1)·(1 − i/len)^power</c>, the seed carried
        /// from channel 0 into channel 1. (The JS wraps it in a ConvolverNode; here the caller does.)
        /// </summary>
        public static AudioBuffer MakeRoom(int sampleRate, double seconds, double power, long seed, string name = null)
        {
            int len = (int)Math.Floor(sampleRate * seconds);
            var ir = new AudioBuffer(2, len, sampleRate) { Name = name };
            long s = seed;
            for (int ch = 0; ch < 2; ch++)
            {
                float[] dd = ir.Channels[ch];
                for (int i = 0; i < len; i++)
                {
                    s = (s * 16807) % 2147483647;
                    dd[i] = (float)(((s / 2147483647d) * 2 - 1) * Math.Pow(1 - (double)i / len, power));
                }
            }
            return ir;
        }

        /// <summary>The design system's damp room and the meta fx room (1.4 s, power 3.2, seed 7).</summary>
        public static AudioBuffer MakeDampRoom(int sampleRate) => MakeRoom(sampleRate, 1.4, 3.2, 7, "room");

        /// <summary>The ambience cave (2.6 s, power 2.4, seed 13).</summary>
        public static AudioBuffer MakeCave(int sampleRate) => MakeRoom(sampleRate, 2.6, 2.4, 13, "cave");

        /// <summary>
        /// The ConvolverNode normalisation (<c>normalize = true</c>, the default; Chromium's Reverb::CalculateNormalizationScale):
        /// <c>power = sqrt(Σ ir² / (channels·length))</c>, at least 0.000125; <c>scale = (1/power)·10^(−58/20)·(44100/sr)</c>,
        /// ×0.5 for a 4-channel (true stereo) impulse.
        /// </summary>
        public static double ConvolverNormalizationScale(AudioBuffer ir)
        {
            const double kGainCalibrationDb = -58, kGainCalibrationSampleRate = 44100, kMinPower = 0.000125;
            double power = 0;
            for (int c = 0; c < ir.NumberOfChannels; c++)
            {
                float[] d = ir.Channels[c];
                double channelPower = 0;
                for (int i = 0; i < d.Length; i++) channelPower += (double)d[i] * d[i];
                power += channelPower;
            }
            power = Math.Sqrt(power / ((double)ir.NumberOfChannels * ir.Length));
            if (double.IsNaN(power) || double.IsInfinity(power) || power < kMinPower) power = kMinPower;
            double scale = 1 / power;
            scale *= Math.Pow(10, 0.05 * kGainCalibrationDb);
            if (ir.SampleRate > 0) scale *= kGainCalibrationSampleRate / ir.SampleRate;
            if (ir.NumberOfChannels == 4) scale *= 0.5;
            return scale;
        }

        /// <summary>FNV-1a (32 bit) over the little-endian bytes of the Float32 samples (the golden data's checksum).</summary>
        public static uint Fnv32(float[] data)
        {
            uint h = 2166136261;
            for (int i = 0; i < data.Length; i++)
            {
                uint bits = (uint)BitConverter.SingleToInt32Bits(data[i]);
                for (int b = 0; b < 4; b++)
                {
                    h ^= (bits >> (8 * b)) & 0xFF;
                    h = unchecked(h * 16777619);
                }
            }
            return h;
        }
    }
}
