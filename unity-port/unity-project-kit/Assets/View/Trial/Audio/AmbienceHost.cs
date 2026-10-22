// The ambience in Unity: an AudioSource that loops a silent stereo clip so that Unity calls this component's OnAudioFilterRead
// on the audio thread, where the ambience engine (Trial.Audio.Synth.AmbienceEngine) renders the live places: beds,
// drones, the seeded sparse voices, the crossfades, the duck and the 2.6 s cave. The ambience is endless and never
// repeats (LFO rates that do not divide each other, a seeded schedule), so it is rendered live, not looped.
// Calls from the main thread are posted to the engine and taken at the start of the next audio block. A device change
// (another output rate) rebuilds the engine with its state and the place fades back in.
using System;
using Trial.Audio.Synth;
using UnityEngine;

namespace Trial.Audio
{
    [DisallowMultipleComponent]
    public sealed class AmbienceHost : MonoBehaviour
    {
        private volatile AmbienceEngine engine;
        private AudioSource source;
        private AudioClip silence;
        private volatile bool faulted;

        /// <summary>The engine rendering now (it is replaced when the output device changes; ask for it each time).</summary>
        public AmbienceEngine Engine => engine;

        /// <summary>
        /// Adds the host and its source to <paramref name="host"/> with an engine at the output rate, the ambience volume
        /// <paramref name="volume"/> and the duck factor <paramref name="duck"/>.
        /// </summary>
        public static AmbienceHost Create(GameObject host, double volume, double duck)
        {
            var h = host.AddComponent<AmbienceHost>();
            h.Build(volume, duck);
            return h;
        }

        private static int OutputRate()
        {
            int sr = AudioSettings.outputSampleRate;
            return sr > 0 ? sr : 48000;
        }

        private void Build(double volume, double duck)
        {
            engine = new AmbienceEngine(OutputRate(), volume, duck);
            source = gameObject.AddComponent<AudioSource>();
            // Stereo, so the filter never gets a mono chain: the cave's two impulse channels are what give the ambience
            // its width, and a one-channel buffer would fold them together (the engine mixes L and R for channels == 1).
            silence = AudioClip.Create("Trial ambience carrier", 1024, 2, OutputRate(), false);
            source.clip = silence;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.priority = 0;
            source.volume = 1;
            source.bypassReverbZones = true;
            source.Play();
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            AmbienceEngine old = engine;
            if (old == null) return;
            engine = old.CloneAt(OutputRate());
            if (source && !source.isPlaying) source.Play();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            AmbienceEngine e = engine;
            if (e == null || faulted || channels <= 0)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            try
            {
                e.Render(data, data.Length / channels, channels);
            }
            catch (Exception ex)
            {
                // A broken ambience must not take the audio thread down: go silent and say so once.
                faulted = true;
                Array.Clear(data, 0, data.Length);
                Debug.LogException(ex);
            }
        }

        private void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            engine?.Destroy();
            engine = null;
            if (source) source.Stop();
            if (silence)
            {
                if (Application.isPlaying) Destroy(silence);
                else DestroyImmediate(silence);
            }
        }
    }
}
