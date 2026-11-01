// The engine-free Web Audio subset the trial's sound is written against: a context that renders 128-frame quanta by
// pulling the graph from its destination, the node factories with the Web Audio names, and an optional call log in the
// format of the JS recording mock (unity-port/tools/audio-golden.mjs), so a port can be compared call by call.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Trial.Audio.Synth
{
    /// <summary>Receives every graph call (node creation, types, buffers, connections, param calls, start / stop).</summary>
    public interface ICallLog
    {
        /// <summary>One call on <paramref name="node"/>: the operation and its arguments (numbers, strings, bools, nodes, params).</summary>
        void Record(AudioNode node, params object[] entry);
    }

    /// <summary>
    /// A call log in the golden format: <c>[id, op, …]</c>, numbers rounded to 1e-6 as the JS mock rounds them. Nodes made after
    /// <see cref="BeginSegment"/> are <c>Kind+n</c> (n from the first node of the segment); older nodes carry their
    /// <see cref="AudioNode.LogName"/> (fx, fxWet, out, wet, …) or <c>Kind#index</c>.
    /// </summary>
    public sealed class RecordingCallLog : ICallLog
    {
        public readonly List<object[]> Entries = new List<object[]>();
        private int baseIndex = int.MaxValue;

        public void BeginSegment(AudioContextLite ctx)
        {
            Entries.Clear();
            baseIndex = ctx.NextNodeIndex;
        }

        public string IdOf(AudioNode n) =>
            n.Index >= baseIndex ? n.Kind + "+" + (n.Index - baseIndex).ToString(CultureInfo.InvariantCulture)
            : n.LogName ?? n.Kind + "#" + n.Index.ToString(CultureInfo.InvariantCulture);

        public void Record(AudioNode node, params object[] entry)
        {
            var e = new object[entry.Length + 1];
            e[0] = IdOf(node);
            for (int i = 0; i < entry.Length; i++) e[i + 1] = Format(entry[i]);
            Entries.Add(e);
        }

        private object Format(object x)
        {
            switch (x)
            {
                case AudioParam p: return IdOf(p.Owner) + "." + p.Name;
                case AudioNode n: return IdOf(n);
                case double d: return Round6(d);
                case float f: return Round6(f);
                case int i: return (double)i;
                default: return x;
            }
        }

        /// <summary>js: <c>Math.round(x·1e6) / 1e6</c> (V8 rounds half up: floor, then +1 when the fraction is ≥ 0.5).</summary>
        public static double Round6(double x)
        {
            double y = x * 1e6;
            double f = Math.Floor(y);
            if (y - f >= 0.5) f += 1;
            return f / 1e6;
        }
    }

    /// <summary>
    /// An offline / pull-driven AudioContext: <see cref="CurrentTime"/> advances by whole render quanta as the destination is
    /// rendered. Nodes made while <see cref="TransientNodes"/> is on are dropped from the graph once they can no longer sound
    /// (a source after its stop, a filter or gain whose inputs are gone and whose tail has passed), so a live graph that keeps
    /// adding voices does not grow.
    /// </summary>
    public sealed class AudioContextLite
    {
        public const int RenderQuantumFrames = 128;

        public int SampleRate { get; }
        public double Nyquist => SampleRate / 2.0;
        /// <summary>The first frame of the next quantum to render.</summary>
        public long CurrentFrame { get; private set; }
        /// <summary>js: <c>ctx.currentTime</c>.</summary>
        public double CurrentTime => (double)CurrentFrame / SampleRate;
        public DestinationNode Destination { get; }
        public ICallLog Log { get; set; }
        /// <summary>Nodes made while this is on are pruned once silent for good (voices; never buses).</summary>
        public bool TransientNodes { get; set; }
        /// <summary>The latest time any source of this context was told to stop (render length helper; reset it to measure one play).</summary>
        public double LatestStopTime { get; set; }

        internal int NextNodeIndex { get; private set; }
        internal long QuantumIndex { get; private set; } = -1;

        private readonly float[] quantumL = new float[RenderQuantumFrames], quantumR = new float[RenderQuantumFrames];
        private int quantumRead = RenderQuantumFrames;

        public AudioContextLite(int sampleRate = 48000, ICallLog log = null)
        {
            if (sampleRate < 3000 || sampleRate > 768000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            SampleRate = sampleRate;
            Log = log;
            Destination = new DestinationNode(this);
        }

        internal int TakeNodeIndex() => NextNodeIndex++;

        /// <summary>
        /// The first frame at or after <paramref name="time"/>: <c>ceil(time·sr)</c> with no tolerance, as Chromium rounds a start or
        /// stop time (so 0.543 s at 48 kHz, 26064.000000000004 frames, starts on frame 26065, as it does in the browser).
        /// </summary>
        public long FrameAtOrAfter(double time) => (long)Math.Ceiling(time * SampleRate);

        // ---- factories (the Web Audio names) ----------------------------------------------------------------------

        public GainNode CreateGain() => new GainNode(this);
        public BiquadFilterNode CreateBiquadFilter() => new BiquadFilterNode(this);
        public OscillatorNode CreateOscillator() => new OscillatorNode(this);
        public AudioBufferSourceNode CreateBufferSource() => new AudioBufferSourceNode(this);
        public StereoPannerNode CreateStereoPanner() => new StereoPannerNode(this);
        /// <param name="blockSize">The convolution partition (a power of two ≥ 128). 128 = zero latency; larger blocks cost less
        /// and delay the output by <c>blockSize</c> frames (<see cref="ConvolverNode.LatencyFrames"/>).</param>
        public ConvolverNode CreateConvolver(int blockSize = RenderQuantumFrames) => new ConvolverNode(this, blockSize);
        public DynamicsCompressorNode CreateDynamicsCompressor() => new DynamicsCompressorNode(this);
        public AudioBuffer CreateBuffer(int numberOfChannels, int length, int sampleRate) => new AudioBuffer(numberOfChannels, length, sampleRate);

        // ---- rendering ---------------------------------------------------------------------------------------------

        /// <summary>Renders one quantum of the destination into <paramref name="left"/> / <paramref name="right"/> (128 frames each).</summary>
        public void RenderQuantum(float[] left, float[] right)
        {
            QuantumIndex++;
            Destination.Pull(QuantumIndex);
            Array.Copy(Destination.Out[0], left, RenderQuantumFrames);
            Array.Copy(Destination.Out[1], right, RenderQuantumFrames);
            CurrentFrame += RenderQuantumFrames;
        }

        /// <summary>
        /// Renders <paramref name="frames"/> frames into planar buffers from <paramref name="offset"/>. Frames left over from a
        /// quantum are kept for the next call, so any block size works.
        /// </summary>
        public void Render(float[] left, float[] right, int offset, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                if (quantumRead >= RenderQuantumFrames)
                {
                    RenderQuantum(quantumL, quantumR);
                    quantumRead = 0;
                }
                left[offset + i] = quantumL[quantumRead];
                right[offset + i] = quantumR[quantumRead];
                quantumRead++;
            }
        }

        /// <summary>
        /// Renders <paramref name="frames"/> frames interleaved over <paramref name="channels"/> (1: the mono mix (L+R)/2;
        /// 2: L, R; more: L, R and silence).
        /// </summary>
        public void Render(float[] interleaved, int frames, int channels)
        {
            for (int i = 0; i < frames; i++)
            {
                if (quantumRead >= RenderQuantumFrames)
                {
                    RenderQuantum(quantumL, quantumR);
                    quantumRead = 0;
                }
                int o = i * channels;
                if (channels == 1) interleaved[o] = 0.5f * (quantumL[quantumRead] + quantumR[quantumRead]);
                else
                {
                    interleaved[o] = quantumL[quantumRead];
                    interleaved[o + 1] = quantumR[quantumRead];
                    for (int c = 2; c < channels; c++) interleaved[o + c] = 0;
                }
                quantumRead++;
            }
        }

        /// <summary>Renders (and drops) whole quanta until <see cref="CurrentTime"/> reaches <paramref name="time"/> (tests, warm-up).</summary>
        public void RenderUntil(double time)
        {
            while (CurrentTime < time - 1e-12)
            {
                RenderQuantum(quantumL, quantumR);
                quantumRead = RenderQuantumFrames;
            }
        }
    }
}
