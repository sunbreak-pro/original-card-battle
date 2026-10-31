// The one-shot voices: a pool of 2D AudioSources that play the rendered clips at an exact DSP time (PlayScheduled), so a
// delayed sound (a step's result 280 ms later, the heartbeat's 0 / 900 / 1800 ms) keeps its spacing to the sample.
// The effects volume is the sources' volume, glided toward its target with the JS's 30 ms time constant
// (Audio.js:698 ramp: setTargetAtTime(v, now, 0.03)). When every source is busy the one that started first is taken.
// The component also gives TrialAudio its frame (Update) and tells it when the GameObject goes away.
using System;
using UnityEngine;

namespace Trial.Audio
{
    [DisallowMultipleComponent]
    public sealed class SfxPlayer : MonoBehaviour
    {
        /// <summary>Voices in the pool (Unity's real voices are 32 in this project; the ambience takes one).</summary>
        public const int PoolSize = 24;
        /// <summary>js: the effects gain's ramp time constant (Audio.js:698).</summary>
        public const float VolumeTimeConstant = 0.03f;

        private AudioSource[] pool;
        private double[] startAt, busyUntil;
        private float volume = 1, target = 1;

        /// <summary>Raised every frame (Update), before nothing else of the trial's sound: TrialAudio's pump.</summary>
        public event Action Frame;
        /// <summary>Raised when the component is destroyed (the GameObject went away, or play mode ended).</summary>
        public event Action Destroyed;

        /// <summary>The effects volume the sources play at now, and the one it glides to.</summary>
        public float Volume => volume;
        public float TargetVolume => target;

        /// <summary>Adds the player and its pool to <paramref name="host"/> at the effects volume <paramref name="effects"/> (no glide at start).</summary>
        public static SfxPlayer Create(GameObject host, float effects)
        {
            var p = host.AddComponent<SfxPlayer>();
            p.Build(effects);
            return p;
        }

        private void Build(float effects)
        {
            volume = target = Mathf.Clamp01(effects);
            pool = new AudioSource[PoolSize];
            startAt = new double[PoolSize];
            busyUntil = new double[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 0;
                s.dopplerLevel = 0;
                s.priority = 64;
                s.volume = volume;
                s.bypassReverbZones = true;
                pool[i] = s;
                startAt[i] = double.NegativeInfinity;
                busyUntil[i] = double.NegativeInfinity;
            }
        }

        /// <summary>Glides the effects volume to <paramref name="effects"/> (0..1) with a 30 ms time constant.</summary>
        public void SetVolume(float effects) => target = Mathf.Clamp01(effects);

        /// <summary>
        /// Plays <paramref name="clip"/> at the DSP time <paramref name="dspTime"/> (a time already past plays at once), starting
        /// <paramref name="offsetSeconds"/> into the clip. False when there is nothing to play.
        /// </summary>
        public bool Play(AudioClip clip, double dspTime, double offsetSeconds = 0)
        {
            if (!clip || pool == null) return false;
            // A start past the clip's end has nothing to play: refuse it before a busy voice is taken for it (compared as a
            // double, so a far start cannot overflow the frame count).
            double offsetFrames = Math.Round(Math.Max(0, double.IsNaN(offsetSeconds) ? 0 : offsetSeconds) * clip.frequency);
            if (!(offsetFrames < clip.samples)) return false;
            int offset = (int)offsetFrames;
            double now = AudioSettings.dspTime;
            int pick = -1;
            for (int i = 0; i < pool.Length; i++)
            {
                if (busyUntil[i] <= now && pool[i] && !pool[i].isPlaying) { pick = i; break; }
            }
            if (pick < 0)
            {
                // every voice is busy: take the one that started first
                double first = double.PositiveInfinity;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (startAt[i] < first) { first = startAt[i]; pick = i; }
                }
            }
            AudioSource s = pool[pick];
            if (!s) return false;
            s.Stop();
            s.clip = clip;
            s.volume = volume;
            s.timeSamples = offset;
            double at = Math.Max(now, dspTime);
            s.PlayScheduled(at);
            startAt[pick] = at;
            busyUntil[pick] = at + (double)(clip.samples - offset) / clip.frequency;
            return true;
        }

        /// <summary>The voices still sounding or waiting for their time.</summary>
        public int Busy
        {
            get
            {
                if (pool == null) return 0;
                double now = AudioSettings.dspTime;
                int n = 0;
                for (int i = 0; i < pool.Length; i++) if (busyUntil[i] > now) n++;
                return n;
            }
        }

        /// <summary>Silences every voice at once.</summary>
        public void StopAll()
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i]) pool[i].Stop();
                busyUntil[i] = double.NegativeInfinity;
                startAt[i] = double.NegativeInfinity;
            }
        }

        private void Update()
        {
            if (pool != null && volume != target)
            {
                float k = 1 - Mathf.Exp(-Time.unscaledDeltaTime / VolumeTimeConstant);
                volume += (target - volume) * k;
                if (Mathf.Abs(target - volume) < 1e-5f) volume = target;
                for (int i = 0; i < pool.Length; i++) if (pool[i]) pool[i].volume = volume;
            }
            Frame?.Invoke();
        }

        private void OnDestroy()
        {
            Action d = Destroyed;
            Destroyed = null;
            Frame = null;
            d?.Invoke();
        }
    }
}
