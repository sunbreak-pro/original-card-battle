// The node base of the Web Audio subset, and the simple nodes: gain, the equal-power stereo panner, the destination and
// the scheduled-source base (start / stop on the first frame at or after the time).
// Channels follow the Web Audio rules the trial relies on: a node outputs as many channels as its widest input
// (channelCountMode "max"), a mono input summed into a stereo one is up-mixed as L = R = mono (no −3 dB), a param input
// is down-mixed to mono as (L + R) / 2, and the destination is stereo.
using System;
using System.Collections.Generic;

namespace Trial.Audio.Synth
{
    public abstract class AudioNode
    {
        public AudioContextLite Context { get; }
        /// <summary>The creation index in the context (the JS mock's <c>ctx.nodes.length</c>).</summary>
        public int Index { get; }
        /// <summary>The JS mock's kind: Gain, Biquad, Osc, BufferSource, Convolver, Compressor, Panner, Destination.</summary>
        public string Kind { get; }
        /// <summary>A bus name for call logs (fx, fxWet, out, wet, amb, …); null for voice nodes.</summary>
        public string LogName { get; set; }
        /// <summary>Pruned from the graph once it can no longer sound (set from <see cref="AudioContextLite.TransientNodes"/>).</summary>
        public bool Transient { get; set; }

        internal readonly List<AudioNode> Inputs = new List<AudioNode>();
        internal readonly List<object> Outputs = new List<object>();

        /// <summary>The nodes connected into this one now (finished transient voices are dropped as the context renders).</summary>
        public int InputCount => Inputs.Count;
        internal readonly float[][] Out = { new float[AudioContextLite.RenderQuantumFrames], new float[AudioContextLite.RenderQuantumFrames] };
        internal int OutChannels = 1;

        private long lastQuantum = -1;
        private bool hadInput;
        private long inputsGoneAt = -1;

        protected AudioNode(AudioContextLite context, string kind)
        {
            Context = context;
            Kind = kind;
            Index = context.TakeNodeIndex();
            Transient = context.TransientNodes;
            context.Log?.Record(this, "new");
        }

        /// <summary>How long the node can still sound after its inputs are gone (a filter's ring, a convolver's impulse).</summary>
        protected virtual long TailFrames => 0;

        /// <summary>js: <c>node.connect(dst)</c>; returns <paramref name="destination"/> so calls chain as in the JS.</summary>
        public T Connect<T>(T destination) where T : AudioNode
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Context != Context) throw new InvalidOperationException("connect across contexts");
            Context.Log?.Record(this, "connect", destination);
            Outputs.Add(destination);
            destination.Inputs.Add(this);
            destination.hadInput = true;
            destination.inputsGoneAt = -1;
            return destination;
        }

        /// <summary>js: <c>node.connect(param)</c>: the node's output is added to the param's value.</summary>
        public void Connect(AudioParam param)
        {
            if (param == null) throw new ArgumentNullException(nameof(param));
            if (param.Owner.Context != Context) throw new InvalidOperationException("connect across contexts");
            Context.Log?.Record(this, "connect", param);
            Outputs.Add(param);
            param.Inputs.Add(this);
        }

        /// <summary>js: <c>node.disconnect()</c>: removes every outgoing connection.</summary>
        public void Disconnect()
        {
            Context.Log?.Record(this, "disconnect");
            for (int i = Outputs.Count - 1; i >= 0; i--)
            {
                object o = Outputs[i];
                if (o is AudioNode n) RemoveInput(n, this);
                else if (o is AudioParam p) p.Inputs.Remove(this);
            }
            Outputs.Clear();
        }

        internal static void Unlink(AudioNode source, AudioNode destination)
        {
            RemoveInput(destination, source);
            source.Outputs.Remove(destination);
        }

        internal static void Unlink(AudioNode source, AudioParam destination)
        {
            destination.Inputs.Remove(source);
            source.Outputs.Remove(destination);
        }

        private static void RemoveInput(AudioNode destination, AudioNode source)
        {
            destination.Inputs.Remove(source);
            if (destination.Inputs.Count == 0) destination.inputsGoneAt = destination.Context.CurrentFrame;
        }

        /// <summary>True once the node can no longer sound from <paramref name="frame"/> on (transient nodes only).</summary>
        internal virtual bool IsFinished(long frame)
        {
            if (!Transient || !hadInput || Inputs.Count > 0) return false;
            if (inputsGoneAt < 0) inputsGoneAt = frame;
            return frame - inputsGoneAt >= TailFrames;
        }

        /// <summary>Computes the node's output for <paramref name="quantum"/> once (inputs first).</summary>
        internal void Pull(long quantum)
        {
            if (lastQuantum == quantum) return;
            lastQuantum = quantum;
            long start = Context.CurrentFrame;
            for (int i = 0; i < Inputs.Count; i++) Inputs[i].Pull(quantum);
            Process(start, quantum);
            long next = start + AudioContextLite.RenderQuantumFrames;
            for (int i = Inputs.Count - 1; i >= 0; i--)
            {
                if (Inputs[i].IsFinished(next))
                {
                    AudioNode gone = Inputs[i];
                    gone.Outputs.Remove(this);
                    Inputs.RemoveAt(i);
                    if (Inputs.Count == 0) inputsGoneAt = next;
                }
            }
        }

        /// <summary>Fills <see cref="Out"/> and <see cref="OutChannels"/> for the quantum that starts at <paramref name="startFrame"/>.</summary>
        protected abstract void Process(long startFrame, long quantum);

        /// <summary>The sum of the inputs (already pulled) into <paramref name="dst"/>, up-mixing mono into stereo; returns the channels.</summary>
        protected int MixInputs(float[][] dst, int maxChannels = 2)
        {
            int n = AudioContextLite.RenderQuantumFrames;
            int channels = 1;
            for (int i = 0; i < Inputs.Count; i++) channels = Math.Max(channels, Inputs[i].OutChannels);
            if (channels > maxChannels) channels = maxChannels;
            Array.Clear(dst[0], 0, n);
            if (channels > 1) Array.Clear(dst[1], 0, n);
            for (int i = 0; i < Inputs.Count; i++)
            {
                AudioNode input = Inputs[i];
                float[] l = input.Out[0];
                if (input.OutChannels == 1)
                {
                    float[] d0 = dst[0];
                    for (int k = 0; k < n; k++) d0[k] += l[k];
                    if (channels > 1)
                    {
                        float[] d1 = dst[1];
                        for (int k = 0; k < n; k++) d1[k] += l[k];
                    }
                }
                else
                {
                    float[] r = input.Out[1];
                    if (channels > 1)
                    {
                        float[] d0 = dst[0], d1 = dst[1];
                        for (int k = 0; k < n; k++) { d0[k] += l[k]; d1[k] += r[k]; }
                    }
                    else
                    {
                        float[] d0 = dst[0];
                        for (int k = 0; k < n; k++) d0[k] += 0.5f * (l[k] + r[k]);
                    }
                }
            }
            return channels;
        }

        protected AudioParam NewParam(string name, double defaultValue, double min = -3.4028234663852886e38, double max = 3.4028234663852886e38) =>
            new AudioParam(this, name, defaultValue, min, max);
    }

    /// <summary>js: <c>ctx.createGain()</c>: output = input × gain (a-rate).</summary>
    public sealed class GainNode : AudioNode
    {
        public readonly AudioParam Gain;

        internal GainNode(AudioContextLite context) : base(context, "Gain")
        {
            Gain = NewParam("gain", 1);
        }

        protected override void Process(long startFrame, long quantum)
        {
            int n = AudioContextLite.RenderQuantumFrames;
            OutChannels = MixInputs(Out);
            bool constant = Gain.Fill(startFrame, quantum);
            float[] g = Gain.Values;
            for (int c = 0; c < OutChannels; c++)
            {
                float[] d = Out[c];
                if (constant)
                {
                    float v = g[0];
                    if (v != 1) for (int k = 0; k < n; k++) d[k] *= v;
                }
                else for (int k = 0; k < n; k++) d[k] *= g[k];
            }
        }
    }

    /// <summary>
    /// js: <c>ctx.createStereoPanner()</c>: equal-power panning (spec). Mono in: x = (pan + 1)/2, L = in·cos(xπ/2),
    /// R = in·sin(xπ/2). Stereo in: pan ≤ 0 moves R into L, pan &gt; 0 moves L into R.
    /// </summary>
    public sealed class StereoPannerNode : AudioNode
    {
        public readonly AudioParam Pan;
        private readonly float[][] input = { new float[AudioContextLite.RenderQuantumFrames], new float[AudioContextLite.RenderQuantumFrames] };

        internal StereoPannerNode(AudioContextLite context) : base(context, "Panner")
        {
            Pan = NewParam("pan", 0, -1, 1);
        }

        protected override void Process(long startFrame, long quantum)
        {
            int n = AudioContextLite.RenderQuantumFrames;
            int ch = MixInputs(input);
            Pan.Fill(startFrame, quantum);
            float[] p = Pan.Values, outL = Out[0], outR = Out[1];
            OutChannels = 2;
            for (int k = 0; k < n; k++)
            {
                double pan = p[k];
                if (ch == 1)
                {
                    double x = (pan + 1) / 2;
                    double a = x * Math.PI / 2;
                    float s = input[0][k];
                    outL[k] = (float)(s * Math.Cos(a));
                    outR[k] = (float)(s * Math.Sin(a));
                }
                else
                {
                    double x = pan <= 0 ? pan + 1 : pan;
                    double a = x * Math.PI / 2;
                    double gl = Math.Cos(a), gr = Math.Sin(a);
                    float l = input[0][k], r = input[1][k];
                    if (pan <= 0)
                    {
                        outL[k] = (float)(l + r * gl);
                        outR[k] = (float)(r * gr);
                    }
                    else
                    {
                        outL[k] = (float)(l * gl);
                        outR[k] = (float)(r + l * gr);
                    }
                }
            }
        }
    }

    /// <summary>js: <c>ctx.destination</c>: the stereo sum of everything connected to it.</summary>
    public sealed class DestinationNode : AudioNode
    {
        internal DestinationNode(AudioContextLite context) : base(context, "Destination")
        {
        }

        protected override void Process(long startFrame, long quantum)
        {
            int ch = MixInputs(Out);
            if (ch == 1) Array.Copy(Out[0], Out[1], AudioContextLite.RenderQuantumFrames);
            OutChannels = 2;
        }
    }

    /// <summary>The base of oscillators and buffer sources: one start, then stop (the last stop wins).</summary>
    public abstract class ScheduledSourceNode : AudioNode
    {
        internal long StartFrame = long.MaxValue, StopFrame = long.MaxValue;
        public bool Started { get; private set; }
        public double StartTime { get; private set; } = double.NaN;
        public double StopTime { get; private set; } = double.NaN;

        protected ScheduledSourceNode(AudioContextLite context, string kind) : base(context, kind)
        {
        }

        protected void StartAt(double when, double offset)
        {
            if (Started) throw new InvalidOperationException("InvalidStateError: start twice");
            if (double.IsNaN(when) || double.IsInfinity(when) || when < 0) throw new ArgumentOutOfRangeException(nameof(when), "bad start " + when);
            if (double.IsNaN(offset) || double.IsInfinity(offset) || offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "bad offset " + offset);
            Context.Log?.Record(this, "start", when, offset);
            Started = true;
            StartTime = when;
            long frame = Context.FrameAtOrAfter(when);
            StartFrame = Math.Max(Context.CurrentFrame, frame);
            // Chromium's start_frame_offset: how far before its first frame the true start lies (≤ 0, in frames).
            StartFrameOffset = when * Context.SampleRate - frame;
        }

        /// <summary><c>start·sr − ceil(start·sr)</c> (≤ 0): the sub-sample part of the start (Chromium's start_frame_offset).</summary>
        internal double StartFrameOffset;

        /// <summary>js: <c>source.stop(when)</c>.</summary>
        public void Stop(double when = 0)
        {
            if (!Started) throw new InvalidOperationException("InvalidStateError: stop before start");
            if (double.IsNaN(when) || double.IsInfinity(when) || when < 0) throw new ArgumentOutOfRangeException(nameof(when), "bad stop " + when);
            Context.Log?.Record(this, "stop", when);
            StopTime = when;
            StopFrame = Math.Max(Context.CurrentFrame, Context.FrameAtOrAfter(when));
            if (when > Context.LatestStopTime) Context.LatestStopTime = when;
        }

        internal override bool IsFinished(long frame) => Transient && Started && StopFrame <= frame || Ended && Transient;

        /// <summary>A buffer source that played its buffer out without looping.</summary>
        protected bool Ended { get; set; }
    }
}
