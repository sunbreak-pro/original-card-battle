// js: ctx.createBufferSource(): plays a buffer from an offset (seconds), looping over the whole buffer when `loop` is
// set, until stop(). A fractional read position is read linearly between frames (Chromium does the same).
using System;

namespace Trial.Audio.Synth
{
    public sealed class AudioBufferSourceNode : ScheduledSourceNode
    {
        public readonly AudioParam PlaybackRate;
        private AudioBuffer buffer;
        private bool loop;
        private double readIndex;
        private bool positioned;
        private double offsetSeconds;

        internal AudioBufferSourceNode(AudioContextLite context) : base(context, "BufferSource")
        {
            PlaybackRate = NewParam("playbackRate", 1);
        }

        /// <summary>js: <c>source.buffer = …</c> (logged by the buffer's <see cref="AudioBuffer.Name"/>).</summary>
        public AudioBuffer Buffer
        {
            get => buffer;
            set
            {
                buffer = value;
                Context.Log?.Record(this, "buffer", value?.Name);
            }
        }

        /// <summary>js: <c>source.loop = true</c>.</summary>
        public bool Loop
        {
            get => loop;
            set
            {
                loop = value;
                Context.Log?.Record(this, "loop", value);
            }
        }

        /// <summary>js: <c>source.start(when, offset)</c>.</summary>
        public void Start(double when = 0, double offset = 0)
        {
            if (buffer == null) throw new InvalidOperationException("buffer source without buffer");
            StartAt(when, offset);
            offsetSeconds = offset;
        }

        protected override void Process(long startFrame, long quantum)
        {
            int n = AudioContextLite.RenderQuantumFrames;
            int channels = buffer == null ? 1 : buffer.NumberOfChannels;
            OutChannels = Math.Min(2, channels);
            for (int c = 0; c < OutChannels; c++) Array.Clear(Out[c], 0, n);
            bool rateConstant = PlaybackRate.Fill(startFrame, quantum);
            long end = startFrame + n;
            if (!Started || Ended || StartFrame >= end || StopFrame <= startFrame || StopFrame <= StartFrame) return;
            int len = buffer.Length;
            double srRatio = (double)buffer.SampleRate / Context.SampleRate;
            if (!positioned)
            {
                // Chromium: the offset is rounded to the nearest frame (so a rate-1 source reads the PCM as stored), and a start
                // between two frames moves the read position on by the part of a frame already gone (sub-sample accurate start).
                double offsetFrames = Math.Min(Math.Max(0, offsetSeconds), buffer.Duration) * buffer.SampleRate;
                readIndex = Math.Round(offsetFrames, MidpointRounding.AwayFromZero);
                if (loop && readIndex >= len) readIndex = 0;
                if (StartFrameOffset < 0) readIndex += -StartFrameOffset * (rateConstant ? PlaybackRate.Values[0] : 1) * srRatio;
                positioned = true;
            }
            int k0 = (int)Math.Max(0, StartFrame - startFrame);
            int k1 = (int)Math.Min(n, StopFrame - startFrame);
            for (int k = k0; k < k1; k++)
            {
                if (!loop && readIndex >= len)
                {
                    Ended = true;
                    break;
                }
                int i0 = (int)readIndex;
                double frac = readIndex - i0;
                int i1 = i0 + 1;
                if (i1 >= len) i1 = loop ? 0 : i0;
                for (int c = 0; c < OutChannels; c++)
                {
                    float[] d = buffer.Channels[c];
                    Out[c][k] = frac == 0 ? d[i0] : (float)((1 - frac) * d[i0] + frac * d[i1]);
                }
                double rate = rateConstant ? PlaybackRate.Values[0] : PlaybackRate.Values[k];
                readIndex += rate * srRatio;
                if (loop)
                {
                    if (readIndex >= len) readIndex -= len;
                    else if (readIndex < 0) readIndex += len;
                }
            }
        }
    }
}
