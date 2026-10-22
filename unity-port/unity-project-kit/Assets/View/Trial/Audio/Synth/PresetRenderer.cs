// Renders one named sound to stereo float PCM, deterministically: a fresh context, the voice exactly as the JS builds it
// (the design system's for battle names, the meta synth's otherwise, with the counters the request gives), the dry
// mix rendered sample by sample, then the room: wet = Convolver(LowPass(dry)) with the normalised 1.4 s damp room, and
// out = dry + reverb·wet (the JS sends every voice to the dry bus and to the wet bus alike, so this is the same sum;
// the low-pass is the design system's Q 1 for battle names and the meta synth's Q 0.7 otherwise).
// The output is the sound before the effects volume, the master and the compressor (those stay live in Unity).
using System;
using System.Collections.Generic;

namespace Trial.Audio.Synth
{
    /// <summary>What to render. Counters are their values before the play (0, 0, 0 = the first play of a fresh synth).</summary>
    public sealed class SoundRequest
    {
        public string Name;
        /// <summary>Silence before the voice (the JS delay); a clip normally renders at 0 and is scheduled later.</summary>
        public double DelayMs;
        /// <summary>Volume multiplier. Battle names: <c>mult || 1</c>; meta names: <c>mult ?? 1</c>.</summary>
        public double? Mult;
        /// <summary>Meta voices: the stereo pan (a step without one alternates by <see cref="StepTurn"/>).</summary>
        public double? Pan;
        /// <summary>Meta steps: the ground (null: "stone", the JS's start value).</summary>
        public string Surface;
        /// <summary>Meta voices: a pitch factor (the miasma lines), <c>pitchMul || 1</c>.</summary>
        public double? PitchMul;
        /// <summary>The play counter before the play (the design system's <c>turn</c> or the meta <c>turn</c>).</summary>
        public int Turn;
        /// <summary>Meta: the shared noise-burst counter before the play.</summary>
        public int NoiseTurn;
        /// <summary>Meta: the step counter before the play.</summary>
        public int StepTurn;
        /// <summary>The send into the damp room (the settings' reverb; the game keeps 0.18).</summary>
        public double Reverb = 0.18;
        /// <summary>A tuned preset in place of the default of <see cref="Name"/>.</summary>
        public SoundPreset Preset;

        public SoundRequest Clone() => (SoundRequest)MemberwiseClone();
    }

    public sealed class RenderedSound
    {
        public string Name;
        public int SampleRate;
        public int Frames;
        /// <summary>Stereo, interleaved L R.</summary>
        public float[] Interleaved;
        /// <summary>The latest stop() of the voice in seconds (delay included): the dry part ends here, the room tail follows.</summary>
        public double End;
        public float Peak;
        /// <summary>False when the voice could not be built (unknown name or timbre, or it threw).</summary>
        public bool Ok;

        public float Left(int frame) => Interleaved[2 * frame];
        public float Right(int frame) => Interleaved[2 * frame + 1];
    }

    public static class PresetRenderer
    {
        public const int DefaultSampleRate = 48000;

        private sealed class RoomCache
        {
            public AudioBuffer Impulse;
            public double Scale;
            public NoiseBank Noise;
            public AudioBuffer DsNoise;
        }

        private static readonly Dictionary<int, RoomCache> Rooms = new Dictionary<int, RoomCache>();
        private static readonly object RoomsLock = new object();

        private static RoomCache For(int sampleRate)
        {
            lock (RoomsLock)
            {
                if (!Rooms.TryGetValue(sampleRate, out RoomCache r))
                {
                    AudioBuffer ir = AudioBuffers.MakeDampRoom(sampleRate);
                    r = new RoomCache
                    {
                        Impulse = ir,
                        Scale = AudioBuffers.ConvolverNormalizationScale(ir),
                        Noise = new NoiseBank(sampleRate),
                        DsNoise = AudioBuffers.MakeDsNoise(sampleRate),
                    };
                    Rooms[sampleRate] = r;
                }
                return r;
            }
        }

        /// <summary>Builds the voice of <paramref name="r"/> on <paramref name="ctx"/> into the two buses; false if it did not build.</summary>
        public static bool BuildVoice(AudioContextLite ctx, SoundRequest r, GainNode dry, GainNode wet)
        {
            RoomCache room = For(ctx.SampleRate);
            SoundPreset pr = r.Preset ?? SoundData.DefaultPreset(r.Name);
            if (pr == null) return false;
            // js: Audio.js:717 battle names go to the design system's synth unchanged.
            if (SoundData.IsBattleName(r.Name))
            {
                DsSound ds = DsSound.OnBuses(ctx, dry, wet, room.DsNoise);
                ds.Presets = new Dictionary<string, SoundPreset> { [r.Name] = pr };
                ds.Turn = r.Turn;
                return ds.Play(r.Name, r.DelayMs, r.Mult);
            }
            var meta = new MetaSynth(ctx, dry, wet, room.Noise) { Turn = r.Turn, NoiseTurn = r.NoiseTurn, StepTurn = r.StepTurn, Surface = r.Surface ?? "stone" };
            var o = new VoiceRouting { Pan = r.Pan, Surface = r.Surface, PitchMul = r.PitchMul };
            return meta.PlayPreset(pr, ctx.CurrentTime, r.DelayMs, r.Mult, o);
        }

        /// <summary>The latest stop() of the voice in seconds (delay included), without rendering.</summary>
        public static double VoiceEnd(SoundRequest r, int sampleRate = DefaultSampleRate)
        {
            var ctx = new AudioContextLite(sampleRate);
            GainNode dry = ctx.CreateGain(), wet = ctx.CreateGain();
            BuildVoice(ctx, r, dry, wet);
            return ctx.LatestStopTime;
        }

        /// <summary>The frames a render of a voice ending at <paramref name="end"/> has: the dry part, then the 1.4 s room.</summary>
        public static int FramesFor(double end, int sampleRate) =>
            (int)Math.Ceiling(end * sampleRate - 1e-7) + (int)Math.Floor(sampleRate * SoundData.RoomSeconds);

        /// <summary>Renders <paramref name="r"/> at <paramref name="sampleRate"/> (deterministic: the same request gives the same samples).</summary>
        public static RenderedSound Render(SoundRequest r, int sampleRate = DefaultSampleRate)
        {
            var ctx = new AudioContextLite(sampleRate);
            GainNode dry = ctx.CreateGain(); dry.LogName = "dry";
            GainNode wet = ctx.CreateGain(); wet.LogName = "wet";
            dry.Connect(ctx.Destination);
            ctx.TransientNodes = true;
            bool ok = BuildVoice(ctx, r, dry, wet);
            double end = ctx.LatestStopTime;
            int frames = FramesFor(end, sampleRate);
            var dryL = new float[frames];
            var dryR = new float[frames];
            ctx.Render(dryL, dryR, 0, frames);

            var result = new RenderedSound { Name = r.Name, SampleRate = sampleRate, Frames = frames, End = end, Ok = ok, Interleaved = new float[frames * 2] };
            float[] o = result.Interleaved;
            if (r.Reverb != 0)
            {
                RoomCache room = For(sampleRate);
                bool battle = SoundData.IsBattleName(r.Name);
                // js: bundle.js:723 (lowpass 2400, Q left at the default 1) / Audio.js:299 F('lowpass', 2400) (Q 0.7)
                BiquadCoefficients lp = BiquadCoefficients.Of(BiquadType.Lowpass, 2400, battle ? 1 : 0.7, 0, sampleRate);
                var wl = (float[])dryL.Clone();
                var wr = (float[])dryR.Clone();
                new BiquadState().Process(wl, 0, frames, lp);
                new BiquadState().Process(wr, 0, frames, lp);
                var cl = new float[frames];
                var cr = new float[frames];
                PartitionedConvolver.ConvolveOffline(wl, wr, room.Impulse.Channels, room.Scale, frames, cl, cr);
                float reverb = (float)r.Reverb;
                for (int i = 0; i < frames; i++)
                {
                    o[2 * i] = dryL[i] + reverb * cl[i];
                    o[2 * i + 1] = dryR[i] + reverb * cr[i];
                }
            }
            else
            {
                for (int i = 0; i < frames; i++)
                {
                    o[2 * i] = dryL[i];
                    o[2 * i + 1] = dryR[i];
                }
            }
            float peak = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float a = Math.Abs(o[i]);
                if (a > peak) peak = a;
            }
            result.Peak = peak;
            return result;
        }
    }
}
