// The master stage of the trial's sound, on the AudioListener so it processes the whole mix (the one-shots and the
// ambience): the master gain, then the compressor (scenes/audio/Audio.js:290-293: master → DynamicsCompressor(threshold
// −18, ratio 4) → destination). The gain glides to its target with the JS's ramp, setTargetAtTime(v, now, 0.03), sample
// by sample; muted, the target is 0 (Audio.js:703).
// [deviation] The browser has two compressors (the battle synth's context and the meta / ambience one) and its mute
// silences only the meta context (a battle sound already ringing keeps ringing); here one master stage carries both, so
// mute silences everything at once. The ambience sits about 13 dB under the hits, so the shared detector moves the hits
// by well under 0.5 dB.
using System;
using Trial.Audio.Synth;
using UnityEngine;

namespace Trial.Audio
{
    [DisallowMultipleComponent]
    public sealed class MasterBus : MonoBehaviour
    {
        /// <summary>js: the master gain's ramp time constant (Audio.js:698).</summary>
        public const double TimeConstant = 0.03;

        private volatile float target = 0.8f;
        private volatile DynamicsCompressor compressor;
        private volatile float glide;
        private float gain;
        private volatile bool primed;
        private bool addedListener;

        /// <summary>The gain the mix is at now (audio thread's last value).</summary>
        public float Gain => gain;
        /// <summary>The gain it glides to (0 while muted).</summary>
        public float Target => target;
        /// <summary>True when this bus added the AudioListener it sits on (no listener was in the scene).</summary>
        public bool AddedListener => addedListener;

        /// <summary>
        /// Puts the master stage on the scene's AudioListener (an active and enabled one: a disabled listener hears nothing,
        /// so a stage on it would leave the mix without its master, mute and compressor), or, when there is none, adds a
        /// listener to <paramref name="fallbackHost"/> and puts it there. Returns the bus already on that listener if there is one.
        /// </summary>
        public static MasterBus Attach(GameObject fallbackHost)
        {
            AudioListener listener = null;
            foreach (AudioListener l in UnityEngine.Object.FindObjectsByType<AudioListener>())
            {
                if (l && l.isActiveAndEnabled)
                {
                    listener = l;
                    break;
                }
            }
            bool added = false;
            if (!listener)
            {
                // One listener per GameObject: reuse the host's own if it has one (AddComponent would give null).
                listener = fallbackHost.GetComponent<AudioListener>();
                if (!listener)
                {
                    listener = fallbackHost.AddComponent<AudioListener>();
                    added = true;
                }
            }
            MasterBus bus = listener.GetComponent<MasterBus>();
            if (!bus)
            {
                bus = listener.gameObject.AddComponent<MasterBus>();
                bus.addedListener = added;
            }
            return bus;
        }

        /// <summary>The master volume and mute (main thread): the gain glides to <c>muted ? 0 : master</c>.</summary>
        public void SetTarget(double master, bool muted)
        {
            target = muted ? 0f : (float)Math.Max(0, Math.Min(1, master));
        }

        /// <summary>Jumps to the target at once (js: <c>master = G(muted ? 0 : master)</c> when the graph is made).</summary>
        public void Prime(double master, bool muted)
        {
            SetTarget(master, muted);
            gain = target;
            primed = true;
        }

        private void Awake()
        {
            Configure();
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            compressor = null;
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged) => Configure();

        private void Configure()
        {
            int sr = AudioSettings.outputSampleRate;
            if (sr <= 0) sr = 48000;
            glide = (float)(1 - Math.Exp(-1.0 / (sr * TimeConstant)));
            compressor = DynamicsCompressor.Trial(sr, compressor?.Channels ?? 2);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            DynamicsCompressor c = compressor;
            if (c == null || channels <= 0) return;
            if (c.Channels != channels)
            {
                // The mix has another layout than stereo (mono, quad, 5.1 …): every channel goes through the one detector and
                // the one look-ahead delay, so none of them runs 6 ms ahead of the others. Made once, when the layout is seen.
                c = DynamicsCompressor.Trial(c.SampleRate, channels);
                compressor = c;
            }
            float t = target, k = glide;
            if (!primed)
            {
                gain = t;
                primed = true;
            }
            float g = gain;
            int frames = data.Length / channels;
            for (int f = 0, i = 0; f < frames; f++)
            {
                g += (t - g) * k;
                for (int ch = 0; ch < channels; ch++, i++) data[i] *= g;
            }
            gain = g;
            c.Process(data, frames, channels);
        }
    }
}
