// js: ctx.createDynamicsCompressor() (threshold −18 dB, ratio 4 in both of the trial's contexts; knee 30, attack 3 ms,
// release 250 ms are the Web Audio defaults). A port of Chromium's DynamicsCompressorKernel in float arithmetic: the
// static curve (linear to the threshold, an exponential knee whose k is found by bisection so the slope at
// threshold + knee is 1/ratio, then the ratio), the makeup gain (1/curve(1))^0.6, a 6 ms look-ahead, a detector with a
// fast (2.5 ms) release, an envelope that attacks at a rate set by the deepest compression of the attack and releases
// on the adaptive four-zone curve, the sin() warp of the gain, recomputed every 32 frames.
// A new compressor starts with its detector at 0 (Chromium): run WarmUp before measuring a sound that starts at once.
using System;

namespace Trial.Audio.Synth
{
    public sealed class DynamicsCompressor
    {
        // The Web Audio defaults (DynamicsCompressor.cpp).
        public float Threshold = -24, Knee = 30, Ratio = 12, Attack = 0.003f, Release = 0.25f;
        public float PreDelay = 0.006f, ReleaseZone1 = 0.09f, ReleaseZone2 = 0.16f, ReleaseZone3 = 0.42f, ReleaseZone4 = 0.98f;
        public float PostGain = 0, EffectBlend = 1;

        private const int MaxPreDelayFrames = 1024, MaxPreDelayFramesMask = MaxPreDelayFrames - 1, DefaultPreDelayFrames = 256;
        private const int DivisionFrames = 32;
        private const float PiOverTwo = (float)(Math.PI / 2);
        private const float MeteringReleaseTimeConstant = 0.325f;

        public float SampleRate { get; }
        public int Channels { get; }

        private readonly float[][] preDelayBuffers;
        private int preDelayRead, preDelayWrite, lastPreDelayFrames = DefaultPreDelayFrames;
        private float detectorAverage, compressorGain, meteringGain, maxAttackCompressionDiffDb;
        private readonly float meteringReleaseK;
        // static curve
        private float ratio = -1, slope = -1, linearThreshold = -1, dbThreshold = -1, dbKnee = -1, kneeThreshold = -1, kneeThresholdDb = -1, ykneeThresholdDb = -1, kCurve = -1;
        // per division
        private int divisionLeft;
        private float scaledDesiredGain, envelopeRate, masterLinearGain, dryMix, wetMix, k;
        private float satReleaseFrames;

        public DynamicsCompressor(float sampleRate, int channels = 2)
        {
            SampleRate = sampleRate;
            Channels = channels;
            preDelayBuffers = new float[channels][];
            for (int c = 0; c < channels; c++) preDelayBuffers[c] = new float[MaxPreDelayFrames];
            meteringReleaseK = (float)(1 - Math.Exp(-1 / (sampleRate * MeteringReleaseTimeConstant)));
            Reset();
        }

        /// <summary>The trial's compressor: threshold −18 dB, ratio 4 (vendor/ds/bundle.js:712, scenes/audio/Audio.js:291).</summary>
        public static DynamicsCompressor Trial(float sampleRate, int channels = 2) =>
            new DynamicsCompressor(sampleRate, channels) { Threshold = -18, Ratio = 4 };

        /// <summary>The gain reduction the meter shows (dB, ≤ 0).</summary>
        public float Reduction => meteringGain;

        public void Reset()
        {
            detectorAverage = 0;
            compressorGain = 1;
            meteringGain = 1;
            for (int c = 0; c < preDelayBuffers.Length; c++) Array.Clear(preDelayBuffers[c], 0, MaxPreDelayFrames);
            preDelayRead = 0;
            preDelayWrite = DefaultPreDelayFrames;
            maxAttackCompressionDiffDb = -1;
            divisionLeft = 0;
        }

        /// <summary>Runs <paramref name="seconds"/> of silence through (the state a running context reaches before a sound).</summary>
        public void WarmUp(double seconds = 0.5)
        {
            int frames = (int)(seconds * SampleRate);
            var zeros = new float[frames * Channels];
            Process(zeros, frames, Channels);
        }

        private static float DecibelsToLinear(float db) => MathF.Pow(10, 0.05f * db);
        private static float LinearToDecibels(float linear) => 20 * MathF.Log10(linear);

        private float KneeCurve(float x, float kk)
        {
            if (x < linearThreshold) return x;
            return linearThreshold + (1 - MathF.Exp(-kk * (x - linearThreshold))) / kk;
        }

        /// <summary>The static curve: linear, the knee, then the constant ratio.</summary>
        private float Saturate(float x, float kk)
        {
            if (x < kneeThreshold) return KneeCurve(x, kk);
            float xDb = LinearToDecibels(x);
            float yDb = ykneeThresholdDb + slope * (xDb - kneeThresholdDb);
            return DecibelsToLinear(yDb);
        }

        private float SlopeAt(float x, float kk)
        {
            if (x < linearThreshold) return 1;
            float x2 = x * 1.001f;
            float xDb = LinearToDecibels(x);
            float x2Db = LinearToDecibels(x2);
            float yDb = LinearToDecibels(KneeCurve(x, kk));
            float y2Db = LinearToDecibels(KneeCurve(x2, kk));
            return (y2Db - yDb) / (x2Db - xDb);
        }

        private float KAtSlope(float desiredSlope)
        {
            float xDb = dbThreshold + dbKnee;
            float x = DecibelsToLinear(xDb);
            float minK = 0.1f, maxK = 10000, kk = 5;
            for (int i = 0; i < 15; ++i)
            {
                float s = SlopeAt(x, kk);
                if (s < desiredSlope) maxK = kk;
                else minK = kk;
                kk = MathF.Sqrt(minK * maxK);
            }
            return kk;
        }

        private float UpdateStaticCurveParameters(float dbThresholdIn, float dbKneeIn, float ratioIn)
        {
            if (dbThresholdIn != dbThreshold || dbKneeIn != dbKnee || ratioIn != ratio)
            {
                dbThreshold = dbThresholdIn;
                linearThreshold = DecibelsToLinear(dbThresholdIn);
                dbKnee = dbKneeIn;
                ratio = ratioIn;
                slope = 1 / ratio;
                float kk = KAtSlope(1 / ratio);
                kneeThresholdDb = dbThresholdIn + dbKneeIn;
                kneeThreshold = DecibelsToLinear(kneeThresholdDb);
                ykneeThresholdDb = LinearToDecibels(KneeCurve(kneeThreshold, kk));
                kCurve = kk;
            }
            return kCurve;
        }

        /// <summary>The static gain of the curve and makeup at a steady input level (tests): curve(x)/x · makeup.</summary>
        public float StaticGain(float x)
        {
            float kk = UpdateStaticCurveParameters(Threshold, Knee, Ratio);
            float makeup = MathF.Pow(1 / Saturate(1, kk), 0.6f);
            return Saturate(x, kk) / x * makeup * DecibelsToLinear(PostGain);
        }

        private void SetPreDelayTime(float preDelayTime)
        {
            int frames = (int)(preDelayTime * SampleRate);
            if (frames > MaxPreDelayFrames - 1) frames = MaxPreDelayFrames - 1;
            if (lastPreDelayFrames != frames)
            {
                lastPreDelayFrames = frames;
                for (int c = 0; c < preDelayBuffers.Length; c++) Array.Clear(preDelayBuffers[c], 0, MaxPreDelayFrames);
                preDelayRead = 0;
                preDelayWrite = frames;
            }
        }

        /// <summary>The per-division part of Chromium's Process: the desired gain and the envelope rate.</summary>
        private void BeginDivision()
        {
            float sampleRate = SampleRate;
            dryMix = 1 - EffectBlend;
            wetMix = EffectBlend;
            k = UpdateStaticCurveParameters(Threshold, Knee, Ratio);
            float fullRangeGain = Saturate(1, k);
            float fullRangeMakeupGain = 1 / fullRangeGain;
            fullRangeMakeupGain = MathF.Pow(fullRangeMakeupGain, 0.6f);
            masterLinearGain = DecibelsToLinear(PostGain) * fullRangeMakeupGain;
            float attackTime = Math.Max(0.001f, Attack);
            float attackFrames = attackTime * sampleRate;
            float releaseFrames = sampleRate * Release;
            satReleaseFrames = 0.0025f * sampleRate;
            float y1 = releaseFrames * ReleaseZone1, y2 = releaseFrames * ReleaseZone2, y3 = releaseFrames * ReleaseZone3, y4 = releaseFrames * ReleaseZone4;
            float kA = 0.9999999999999998f * y1 + 1.8432219684323923e-16f * y2 - 1.9373394351676423e-16f * y3 + 8.824516011816245e-18f * y4;
            float kB = -1.5788320352845888f * y1 + 2.3305837032074286f * y2 - 0.9141194204840429f * y3 + 0.1623677525612032f * y4;
            float kC = 0.5334142869106424f * y1 - 1.272736789213631f * y2 + 0.9258856042207512f * y3 - 0.18656310191776226f * y4;
            float kD = 0.08783463138207234f * y1 - 0.1694162967925622f * y2 + 0.08588057951595272f * y3 - 0.00429891410546283f * y4;
            float kE = -0.042416883008123074f * y1 + 0.1115693827987602f * y2 - 0.09764676325265872f * y3 + 0.028494263462021576f * y4;
            SetPreDelayTime(PreDelay);

            if (float.IsNaN(detectorAverage) || float.IsInfinity(detectorAverage)) detectorAverage = 1;
            float desiredGain = detectorAverage;
            scaledDesiredGain = MathF.Asin(desiredGain) / PiOverTwo;
            bool isReleasing = scaledDesiredGain > compressorGain;
            float compressionDiffDb = LinearToDecibels(compressorGain / scaledDesiredGain);
            if (isReleasing)
            {
                maxAttackCompressionDiffDb = -1;
                if (float.IsNaN(compressionDiffDb) || float.IsInfinity(compressionDiffDb)) compressionDiffDb = -1;
                float x = compressionDiffDb;
                x = x < -12 ? -12 : x > 0 ? 0 : x;
                x = 0.25f * (x + 12);
                float x2 = x * x, x3 = x2 * x, x4 = x2 * x2;
                float release = kA + kB * x + kC * x2 + kD * x3 + kE * x4;
                const float SpacingDb = 5;
                float dbPerFrame = SpacingDb / release;
                envelopeRate = DecibelsToLinear(dbPerFrame);
            }
            else
            {
                if (float.IsNaN(compressionDiffDb) || float.IsInfinity(compressionDiffDb)) compressionDiffDb = 1;
                if (maxAttackCompressionDiffDb == -1 || maxAttackCompressionDiffDb < compressionDiffDb) maxAttackCompressionDiffDb = compressionDiffDb;
                float effAttenDiffDb = Math.Max(0.5f, maxAttackCompressionDiffDb);
                float x = 0.25f / effAttenDiffDb;
                envelopeRate = 1 - MathF.Pow(x, 1 / attackFrames);
            }
            divisionLeft = DivisionFrames;
        }

        /// <summary>Compresses <paramref name="frames"/> interleaved frames in place (the first <see cref="Channels"/> channels).</summary>
        public void Process(float[] interleaved, int frames, int stride)
        {
            int channels = Math.Min(Channels, stride);
            for (int f = 0; f < frames; f++)
            {
                if (divisionLeft == 0) BeginDivision();
                int o = f * stride;
                float compressorInput = 0;
                for (int j = 0; j < channels; j++)
                {
                    float undelayed = interleaved[o + j];
                    preDelayBuffers[j][preDelayWrite] = undelayed;
                    float abs = undelayed > 0 ? undelayed : -undelayed;
                    if (compressorInput < abs) compressorInput = abs;
                }
                float total = Step(compressorInput);
                for (int j = 0; j < channels; j++) interleaved[o + j] = preDelayBuffers[j][preDelayRead] * total;
                preDelayRead = (preDelayRead + 1) & MaxPreDelayFramesMask;
                preDelayWrite = (preDelayWrite + 1) & MaxPreDelayFramesMask;
                divisionLeft--;
                if (divisionLeft == 0)
                {
                    if (Math.Abs(detectorAverage) < 1.17549435e-38f) detectorAverage = 0;
                    if (Math.Abs(compressorGain) < 1.17549435e-38f) compressorGain = 0;
                }
            }
        }

        /// <summary>Compresses planar buffers (<paramref name="channels"/> of them) from <paramref name="offset"/>, in place.</summary>
        public void Process(float[][] planar, int channels, int offset, int frames)
        {
            channels = Math.Min(Channels, channels);
            for (int f = offset, end = offset + frames; f < end; f++)
            {
                if (divisionLeft == 0) BeginDivision();
                float compressorInput = 0;
                for (int j = 0; j < channels; j++)
                {
                    float undelayed = planar[j][f];
                    preDelayBuffers[j][preDelayWrite] = undelayed;
                    float abs = undelayed > 0 ? undelayed : -undelayed;
                    if (compressorInput < abs) compressorInput = abs;
                }
                float total = Step(compressorInput);
                for (int j = 0; j < channels; j++) planar[j][f] = preDelayBuffers[j][preDelayRead] * total;
                preDelayRead = (preDelayRead + 1) & MaxPreDelayFramesMask;
                preDelayWrite = (preDelayWrite + 1) & MaxPreDelayFramesMask;
                divisionLeft--;
                if (divisionLeft == 0)
                {
                    if (Math.Abs(detectorAverage) < 1.17549435e-38f) detectorAverage = 0;
                    if (Math.Abs(compressorGain) < 1.17549435e-38f) compressorGain = 0;
                }
            }
        }

        /// <summary>The per-frame inner loop: detector, envelope, warp; returns the total gain for the delayed frame.</summary>
        private float Step(float compressorInput)
        {
            float absInput = compressorInput > 0 ? compressorInput : -compressorInput;
            float shapedInput = Saturate(absInput, k);
            float attenuation = absInput <= 0.0001f ? 1 : shapedInput / absInput;
            float attenuationDb = -LinearToDecibels(attenuation);
            attenuationDb = Math.Max(2.0f, attenuationDb);
            float dbPerFrame = attenuationDb / satReleaseFrames;
            float satReleaseRate = DecibelsToLinear(dbPerFrame) - 1;
            bool isRelease = attenuation > detectorAverage;
            float rate = isRelease ? satReleaseRate : 1;
            detectorAverage += (attenuation - detectorAverage) * rate;
            detectorAverage = Math.Min(1.0f, detectorAverage);
            if (float.IsNaN(detectorAverage) || float.IsInfinity(detectorAverage)) detectorAverage = 1;
            if (envelopeRate < 1)
            {
                compressorGain += (scaledDesiredGain - compressorGain) * envelopeRate;
            }
            else
            {
                compressorGain *= envelopeRate;
                compressorGain = Math.Min(1.0f, compressorGain);
            }
            float postWarpCompressorGain = MathF.Sin(PiOverTwo * compressorGain);
            float totalGain = dryMix + wetMix * masterLinearGain * postWarpCompressorGain;
            float dbRealGain = 20 * MathF.Log10(postWarpCompressorGain);
            if (dbRealGain < meteringGain) meteringGain = dbRealGain;
            else meteringGain += (dbRealGain - meteringGain) * meteringReleaseK;
            return totalGain;
        }
    }

    /// <summary>The compressor as a node: up to 2 channels in, 2 out (the parameters are k-rate, read every quantum).</summary>
    public sealed class DynamicsCompressorNode : AudioNode
    {
        public readonly AudioParam Threshold, Knee, Ratio, Attack, Release;
        private readonly DynamicsCompressor kernel;
        private readonly float[][] planar = { new float[AudioContextLite.RenderQuantumFrames], new float[AudioContextLite.RenderQuantumFrames] };

        internal DynamicsCompressorNode(AudioContextLite context) : base(context, "Compressor")
        {
            Threshold = NewParam("threshold", -24, -100, 0);
            Ratio = NewParam("ratio", 12, 1, 20);
            Knee = NewParam("knee", 30, 0, 40);
            Attack = NewParam("attack", 0.003, 0, 1);
            Release = NewParam("release", 0.25, 0, 1);
            kernel = new DynamicsCompressor(context.SampleRate, 2);
        }

        public float Reduction => kernel.Reduction;

        protected override long TailFrames => Context.SampleRate / 100;

        protected override void Process(long startFrame, long quantum)
        {
            int n = AudioContextLite.RenderQuantumFrames;
            int ch = MixInputs(planar);
            if (ch == 1) Array.Copy(planar[0], planar[1], n);
            Threshold.Fill(startFrame, quantum); Knee.Fill(startFrame, quantum); Ratio.Fill(startFrame, quantum);
            Attack.Fill(startFrame, quantum); Release.Fill(startFrame, quantum);
            kernel.Threshold = Threshold.Values[0];
            kernel.Knee = Knee.Values[0];
            kernel.Ratio = Ratio.Values[0];
            kernel.Attack = Attack.Values[0];
            kernel.Release = Release.Values[0];
            kernel.Process(planar, 2, 0, n);
            Array.Copy(planar[0], Out[0], n);
            Array.Copy(planar[1], Out[1], n);
            OutChannels = 2;
        }
    }
}
