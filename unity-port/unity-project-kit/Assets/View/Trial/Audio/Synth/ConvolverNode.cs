// js: ctx.createConvolver() with a stereo impulse and normalize = true (the default): uniformly partitioned
// overlap-save convolution. Mono in with a stereo impulse gives L = in∗irL, R = in∗irR; stereo in gives
// L = inL∗irL, R = inR∗irR (a mono input is up-mixed L = R first, so both cases are one formula). The two channels go
// through one complex FFT (L + iR) and come back through one inverse.
using System;

namespace Trial.Audio.Synth
{
    /// <summary>
    /// Uniformly partitioned convolution of a (≤ 2 channel) impulse with blocks of <see cref="BlockSize"/> frames. Each call to
    /// <see cref="ProcessBlock"/> takes one block of input and returns the same block of output (no latency at block level).
    /// </summary>
    public sealed class PartitionedConvolver
    {
        public int BlockSize { get; }
        public int Partitions { get; }
        private readonly int fftSize, bins;
        private readonly Fft fft;
        // impulse spectra [channel][partition][bin]
        private readonly double[][][] hr, hi;
        // frequency-domain delay line [channel][slot][bin]
        private readonly double[][][] xr, xi;
        private int slot;
        private readonly float[] prevL, prevR;
        private readonly double[] zr, zi, yr, yi, accLr, accLi, accRr, accRi;

        /// <param name="impulse">1 or 2 channels, already scaled (see <see cref="AudioBuffers.ConvolverNormalizationScale"/>).</param>
        public PartitionedConvolver(float[][] impulse, int blockSize, double scale = 1)
        {
            if (blockSize < 2 || (blockSize & (blockSize - 1)) != 0) throw new ArgumentOutOfRangeException(nameof(blockSize));
            BlockSize = blockSize;
            fftSize = 2 * blockSize;
            bins = blockSize + 1;
            fft = Fft.Of(fftSize);
            int len = 0;
            foreach (float[] ch in impulse) len = Math.Max(len, ch.Length);
            Partitions = Math.Max(1, (len + blockSize - 1) / blockSize);
            float[] irL = impulse[0], irR = impulse.Length > 1 ? impulse[1] : impulse[0];
            hr = new double[2][][]; hi = new double[2][][];
            xr = new double[2][][]; xi = new double[2][][];
            for (int c = 0; c < 2; c++)
            {
                hr[c] = new double[Partitions][]; hi[c] = new double[Partitions][];
                xr[c] = new double[Partitions][]; xi[c] = new double[Partitions][];
                for (int p = 0; p < Partitions; p++)
                {
                    hr[c][p] = new double[bins]; hi[c][p] = new double[bins];
                    xr[c][p] = new double[bins]; xi[c][p] = new double[bins];
                }
            }
            zr = new double[fftSize]; zi = new double[fftSize];
            yr = new double[fftSize]; yi = new double[fftSize];
            for (int p = 0; p < Partitions; p++)
            {
                Array.Clear(zr, 0, fftSize);
                Array.Clear(zi, 0, fftSize);
                int from = p * blockSize;
                for (int i = 0; i < blockSize; i++)
                {
                    int j = from + i;
                    if (j < irL.Length) zr[i] = irL[j] * scale;
                    if (j < irR.Length) zi[i] = irR[j] * scale;
                }
                fft.Forward(zr, zi);
                Split(zr, zi, hr[0][p], hi[0][p], hr[1][p], hi[1][p]);
            }
            prevL = new float[blockSize];
            prevR = new float[blockSize];
            accLr = new double[bins]; accLi = new double[bins];
            accRr = new double[bins]; accRi = new double[bins];
        }

        /// <summary>Splits the spectrum of a + ib (a, b real) into the half spectra of a and b.</summary>
        private void Split(double[] re, double[] im, double[] ar, double[] ai, double[] br, double[] bi)
        {
            int n = fftSize;
            for (int k = 0; k < bins; k++)
            {
                int m = (n - k) & (n - 1);
                double r1 = re[k], i1 = im[k], r2 = re[m], i2 = -im[m]; // Z[k], conj(Z[N−k])
                ar[k] = 0.5 * (r1 + r2); ai[k] = 0.5 * (i1 + i2);
                // (Z[k] − conj(Z[N−k])) / 2i
                double dr = r1 - r2, di = i1 - i2;
                br[k] = 0.5 * di; bi[k] = -0.5 * dr;
            }
        }

        /// <summary>One block: <paramref name="inL"/> / <paramref name="inR"/> (may be the same array) → <paramref name="outL"/> / <paramref name="outR"/>.</summary>
        public void ProcessBlock(float[] inL, float[] inR, int inOffset, float[] outL, float[] outR, int outOffset)
        {
            int b = BlockSize, n = fftSize;
            for (int i = 0; i < b; i++)
            {
                zr[i] = prevL[i]; zi[i] = prevR[i];
                float l = inL[inOffset + i], r = inR[inOffset + i];
                zr[b + i] = l; zi[b + i] = r;
                prevL[i] = l; prevR[i] = r;
            }
            fft.Forward(zr, zi);
            slot = (slot + 1) % Partitions;
            Split(zr, zi, xr[0][slot], xi[0][slot], xr[1][slot], xi[1][slot]);
            // Y_c = Σ_p X_c[slot − p]·H_c[p]
            Array.Clear(yr, 0, n);
            Array.Clear(yi, 0, n);
            Array.Clear(accLr, 0, bins); Array.Clear(accLi, 0, bins);
            Array.Clear(accRr, 0, bins); Array.Clear(accRi, 0, bins);
            for (int p = 0; p < Partitions; p++)
            {
                int s = slot - p;
                if (s < 0) s += Partitions;
                double[] aLr = xr[0][s], aLi = xi[0][s], aRr = xr[1][s], aRi = xi[1][s];
                double[] hLr = hr[0][p], hLi = hi[0][p], hRr = hr[1][p], hRi = hi[1][p];
                for (int k = 0; k < bins; k++)
                {
                    accLr[k] += aLr[k] * hLr[k] - aLi[k] * hLi[k];
                    accLi[k] += aLr[k] * hLi[k] + aLi[k] * hLr[k];
                    accRr[k] += aRr[k] * hRr[k] - aRi[k] * hRi[k];
                    accRi[k] += aRr[k] * hRi[k] + aRi[k] * hRr[k];
                }
            }
            // W = YL + i·YR on the full circle (YL, YR are spectra of real signals).
            for (int k = 0; k < bins; k++)
            {
                yr[k] = accLr[k] - accRi[k];
                yi[k] = accLi[k] + accRr[k];
                if (k > 0 && k < b)
                {
                    // conj(YL[k]) + i·conj(YR[k])
                    yr[n - k] = accLr[k] + accRi[k];
                    yi[n - k] = -accLi[k] + accRr[k];
                }
            }
            fft.Inverse(yr, yi);
            double inv = 1.0 / n;
            for (int i = 0; i < b; i++)
            {
                outL[outOffset + i] = (float)(yr[b + i] * inv);
                outR[outOffset + i] = (float)(yi[b + i] * inv);
            }
        }

        /// <summary>
        /// The whole convolution of <paramref name="inputL"/> / <paramref name="inputR"/> with the impulse, truncated to
        /// <paramref name="frames"/> frames (zero latency: output frame i lines up with input frame i).
        /// </summary>
        public static void ConvolveOffline(float[] inputL, float[] inputR, float[][] impulse, double scale, int frames, float[] outL, float[] outR, int blockSize = 8192)
        {
            var conv = new PartitionedConvolver(impulse, blockSize, scale);
            var bl = new float[blockSize];
            var br = new float[blockSize];
            var ol = new float[blockSize];
            var or = new float[blockSize];
            for (int start = 0; start < frames; start += blockSize)
            {
                int count = Math.Min(blockSize, frames - start);
                for (int i = 0; i < blockSize; i++)
                {
                    int j = start + i;
                    bl[i] = i < count && j < inputL.Length ? inputL[j] : 0;
                    br[i] = i < count && j < inputR.Length ? inputR[j] : 0;
                }
                conv.ProcessBlock(bl, br, 0, ol, or, 0);
                Array.Copy(ol, 0, outL, start, count);
                Array.Copy(or, 0, outR, start, count);
            }
        }
    }

    public sealed class ConvolverNode : AudioNode
    {
        private AudioBuffer buffer;
        private PartitionedConvolver engine;
        private readonly int blockSize;
        private readonly float[][] input = { new float[AudioContextLite.RenderQuantumFrames], new float[AudioContextLite.RenderQuantumFrames] };
        private float[] inL, inR, outL, outR;
        private int pos;

        /// <summary>js: <c>convolver.normalize</c> (true by default; read when the buffer is set).</summary>
        public bool Normalize { get; set; } = true;

        /// <summary>The output lags the input by this many frames (0 for a 128-frame block).</summary>
        public int LatencyFrames => blockSize == AudioContextLite.RenderQuantumFrames ? 0 : blockSize;

        internal ConvolverNode(AudioContextLite context, int blockSize) : base(context, "Convolver")
        {
            if (blockSize < AudioContextLite.RenderQuantumFrames || (blockSize & (blockSize - 1)) != 0)
                throw new ArgumentOutOfRangeException(nameof(blockSize), "a power of two ≥ 128");
            this.blockSize = blockSize;
        }

        /// <summary>js: <c>convolver.buffer = ir</c>.</summary>
        public AudioBuffer Buffer
        {
            get => buffer;
            set
            {
                buffer = value;
                Context.Log?.Record(this, "buffer", value?.Name);
                if (value == null)
                {
                    engine = null;
                    return;
                }
                double scale = Normalize ? AudioBuffers.ConvolverNormalizationScale(value) : 1;
                engine = new PartitionedConvolver(value.Channels, blockSize, scale);
                inL = new float[blockSize]; inR = new float[blockSize];
                outL = new float[blockSize]; outR = new float[blockSize];
                pos = 0;
            }
        }

        protected override long TailFrames => buffer == null ? 0 : buffer.Length + blockSize;

        protected override void Process(long startFrame, long quantum)
        {
            int n = AudioContextLite.RenderQuantumFrames;
            int ch = MixInputs(input);
            if (ch == 1) Array.Copy(input[0], input[1], n);
            OutChannels = 2;
            if (engine == null)
            {
                Array.Clear(Out[0], 0, n);
                Array.Clear(Out[1], 0, n);
                return;
            }
            if (blockSize == n)
            {
                engine.ProcessBlock(input[0], input[1], 0, Out[0], Out[1], 0);
                return;
            }
            // Larger blocks: gather a block, output the previous one (latency = blockSize).
            Array.Copy(input[0], 0, inL, pos, n);
            Array.Copy(input[1], 0, inR, pos, n);
            Array.Copy(outL, pos, Out[0], 0, n);
            Array.Copy(outR, pos, Out[1], 0, n);
            pos += n;
            if (pos == blockSize)
            {
                engine.ProcessBlock(inL, inR, 0, outL, outR, 0);
                pos = 0;
            }
        }
    }
}
