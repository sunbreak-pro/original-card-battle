// js: scenes/audio/Audio.js:219-850 createAudio(): the game's whole sound behind one object (ctx.audio), in PascalCase.
// Battle names (the design system's 29 presets) and the 27 meta presets play by the JS's rules (mute, unlock, the
// design system's hover throttle, the meta gaps, the play counters behind the ±3 % alternation and the left / right
// steps, the step plan, the miasma lines); the sound itself is a pre-rendered clip (SfxClipCache) scheduled on the DSP
// clock (SfxPlayer); the ambience runs live (AmbienceHost → AmbienceEngine); the master gain, mute and compressor sit on
// the listener (MasterBus). Every scene gets the same instance from the router; it lives on a DontDestroyOnLoad object.
// Main thread only.
//
// Differences from the browser, all on purpose:
//  - Unity has no autoplay gate: Create unlocks at once (pass unlock: false to keep the JS's pending start). bindUnlock
//    is not ported.
//  - The gaps and the hover throttle are measured on the real clock (the JS uses the meta context's clock, which stands
//    still only while muted, when nothing plays anyway); delays are added to the DSP clock (the JS adds them to
//    ctx.currentTime).
//  - Mute silences everything at the master stage, a battle sound already ringing included (see MasterBus).
//  - Tuning presets at run time (audio.presets = …, reset()) is the dev page's and is not ported.
using System;
using System.Collections.Generic;
using Trial.Audio.Synth;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Trial.Audio
{
    /// <summary>js: the <c>o</c> of <c>play(name, delayMs, mult, { pitchMul, pan, surface })</c>.</summary>
    public sealed class PlayOptions
    {
        /// <summary>A pitch factor rendered into the voice (not a playback-rate change); null or 0 = 1.</summary>
        public double? PitchMul;
        /// <summary>The stereo pan (−1..1). A step without one alternates right / left.</summary>
        public double? Pan;
        /// <summary>A step's ground (stone, litter, …); null = the current place's.</summary>
        public string Surface;
    }

    /// <summary>The game's sound as every scene uses it (ctx.audio): the createAudio object of the JS.</summary>
    public interface ITrialAudio
    {
        bool Play(string name, double delayMs = 0, double? mult = null, PlayOptions o = null);
        bool Has(string name);
        string[] StepTo(bool fresh = true, double line = 0, bool miasmaDeath = false, string result = null);
        bool MiasmaLine(double linePct, double delayMs = 0);
        bool SetAmbience(object id, double? fadeMs = null);
        void DuckAmbience(double factor, double? fadeMs = null);
        void Duck(bool on, double? fadeMs = null);
        void SetMaster(double v);
        void SetEffects(double v);
        void SetAmbienceVolume(double v);
        void SetReverb(double v);
        void SetMuted(bool v);
        bool ToggleMute();
        void ApplySettings(double? master = null, double? effects = null, double? ambience = null, double? reverb = null, bool? muted = null);
        Action OnChange(Action<SoundSettings> fn);
        SoundSettings Settings { get; }
        bool Muted { get; set; }
        double Master { get; }
        double Effects { get; }
        double AmbienceVolume { get; }
        double Reverb { get; }
        bool Unlocked { get; }
        string Ambience { get; }
        string Surface { get; }
    }

    public sealed class TrialAudio : ITrialAudio
    {
        /// <summary>The instance made last and not destroyed (a convenience for code without a ctx; cleared at every play start).</summary>
        public static TrialAudio Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload is off: an instance left from the last play session must not live on.
            TrialAudio c = Current;
            Current = null;
            if (c == null) return;
            try { c.Destroy(); }
            catch (Exception) { /* its objects are gone already */ }
        }

        private readonly Dictionary<string, SoundPreset> presets;
        private readonly HashSet<string> battleNames;
        private readonly SoundSettings state;
        private readonly List<Action<SoundSettings>> listeners = new List<Action<SoundSettings>>();
        private readonly SoundRules.GapGate gaps = new SoundRules.GapGate();
        private bool unlocked, destroyed, frozen, bootQueued;
        private int turn, dsTurn, stepTurn, errors;
        private double? dsLastHover, freezeAt, dspBase;

        private readonly GameObject root;
        private readonly SfxPlayer player;
        private readonly AmbienceHost ambience;
        private readonly SfxClipCache cache;
        private MasterBus bus;
        private AmbienceEngine lastEngine;

        /// <summary>
        /// js: <c>createAudio(OCB, settings, { onChange })</c>, then <c>unlock()</c> (Unity has no autoplay gate).
        /// <paramref name="settings"/> is a whole snapshot (as <see cref="Settings"/> gives, or
        /// <c>SoundData.GameSoundSettingsDefaults()</c>), normalised; null = the module defaults (effects 1.0).
        /// </summary>
        public static TrialAudio Create(SoundSettings settings = null, Action<SoundSettings> onChange = null, bool unlock = true)
        {
            var a = new TrialAudio(settings, onChange);
            if (unlock) a.Unlock();
            return a;
        }

        /// <summary>
        /// js: <c>createAudio(OCB, save.settings, { onChange })</c> with the app's persisted sound settings (master, effects,
        /// ambience, muted); the reverb is not persisted and stays at 0.18.
        /// </summary>
        public static TrialAudio Create(double master, double effects, double ambience, bool muted, Action<SoundSettings> onChange = null, bool unlock = true)
        {
            var blob = new Dictionary<string, object> { ["master"] = master, ["effects"] = effects, ["ambience"] = ambience, ["muted"] = muted };
            return Create(SoundRules.NormalizeSettings(blob), onChange, unlock);
        }

        // js: scenes/audio/Audio.js:219
        private TrialAudio(SoundSettings settings, Action<SoundSettings> onChange)
        {
            battleNames = new HashSet<string>(SoundData.DsSoundNames, StringComparer.Ordinal);
            state = SoundRules.NormalizeSettings(settings);
            if (onChange != null) listeners.Add(onChange);
            // js: mergeMeta(): one map, the battle presets first.
            presets = SoundData.DsSoundDefaults();
            foreach (KeyValuePair<string, SoundPreset> kv in SoundData.MetaSoundDefaults())
                if (!battleNames.Contains(kv.Key)) presets[kv.Key] = kv.Value;

            // js: ensure(): the graph (here the objects that carry it).
            root = new GameObject("Trial Audio");
            if (Application.isPlaying) Object.DontDestroyOnLoad(root);
            player = SfxPlayer.Create(root, (float)state.Effects);
            player.Frame += Pump;
            player.Destroyed += OnHostDestroyed;
            ambience = AmbienceHost.Create(root, state.Ambience, 1);
            lastEngine = ambience.Engine;
            bus = MasterBus.Attach(root);
            bus.Prime(state.Master, state.Muted);
            int sr = AudioSettings.outputSampleRate;
            cache = new SfxClipCache(sr > 0 ? sr : PresetRenderer.DefaultSampleRate, state.Reverb);
            Current = this;
        }

        // ---- the pieces ---------------------------------------------------------------------------------------------

        private AmbienceEngine Engine
        {
            get
            {
                if (ambience)
                {
                    AmbienceEngine e = ambience.Engine;
                    if (e != null) lastEngine = e;
                }
                return lastEngine;
            }
        }

        private static double RealNow => Time.realtimeSinceStartupAsDouble;

        private double DspNow => dspBase ?? AudioSettings.dspTime;

        /// <summary>The clip cache (its counts, for the dev window and tests).</summary>
        public SfxClipCache Clips => cache;
        /// <summary>The one-shot player (its pool).</summary>
        public SfxPlayer Player => player;
        /// <summary>The master stage on the listener.</summary>
        public MasterBus Bus => bus;
        /// <summary>The ambience host (its engine renders on the audio thread).</summary>
        public AmbienceHost AmbienceHost => ambience;

        // ---- settings (Audio.js:695-710) ----------------------------------------------------------------------------

        // js: scenes/audio/Audio.js:696
        private SoundSettings Snapshot() => state.Clone();

        // js: scenes/audio/Audio.js:697
        private void Changed()
        {
            SoundSettings s = Snapshot();
            foreach (Action<SoundSettings> fn in listeners.ToArray())
            {
                try { fn(s); }
                catch (Exception) { /* ignore */ }
            }
        }

        // js: scenes/audio/Audio.js:699 applyBase(): the battle synth's master (master × effects: the master stage times the
        // players' volume here) and its reverb (baked into the clips).
        private void ApplyBase()
        {
            if (!destroyed) cache.SetReverb(state.Reverb);
        }

        // js: scenes/audio/Audio.js:700
        private void ApplyMute()
        {
            if (destroyed) return;
            if (bus) bus.SetTarget(state.Master, state.Muted);
            if (!unlocked) return;
            freezeAt = null;
            if (state.Muted)
            {
                // Stop rendering a silent ambience after the fade; resume on unmute.
                freezeAt = RealNow + 0.4;
            }
            else if (frozen)
            {
                Engine.SetFrozen(false);
                frozen = false;
            }
        }

        // js: scenes/audio/Audio.js:281 ensure(): the graph exists from Create; a suspended ambience resumes unless muted.
        private bool Ensure()
        {
            if (destroyed) return false;
            if (frozen && !state.Muted)
            {
                Engine.SetFrozen(false);
                frozen = false;
            }
            return true;
        }

        /// <summary>The frame's work (SfxPlayer.Update): new clips, the delayed suspend, a lost listener.</summary>
        private void Pump()
        {
            if (destroyed) return;
            cache.Pump();
            if (freezeAt.HasValue && RealNow >= freezeAt.Value)
            {
                freezeAt = null;
                if (state.Muted && !frozen)
                {
                    Engine.SetFrozen(true);
                    frozen = true;
                }
            }
            if (!bus)
            {
                // the listener it sat on went away with its scene
                bus = MasterBus.Attach(root);
                bus.Prime(state.Master, state.Muted);
            }
        }

        // ---- play (Audio.js:713-737) ----------------------------------------------------------------------------------

        /// <summary>
        /// js: scenes/audio/Audio.js:713 play(name, delayMs, mult, o): plays a battle or meta preset <paramref name="delayMs"/> real
        /// ms from now, its volume × <paramref name="mult"/> (null = the preset's). False when it stays silent: muted,
        /// destroyed, an unknown name, before unlock, inside the name's gap, or a voice that could not be built. A battle name
        /// returns whether the sound is unlocked (the JS returns <c>unlocked</c> for those).
        /// </summary>
        public bool Play(string name, double delayMs = 0, double? mult = null, PlayOptions o = null)
        {
            if (state.Muted || destroyed) return false;
            if (name == null || !presets.TryGetValue(name, out SoundPreset pr)) return false;
            if (battleNames.Contains(name))
            {
                BasePlay(name, pr, delayMs, mult);
                return unlocked;
            }
            if (!unlocked || !Ensure()) return false;
            double now = RealNow;
            if (!gaps.TryAccept(name, pr.Gap, now)) return false;
            turn++;
            if (!MetaSynth.Has(pr.Timbre)) return false;
            double? pan = o?.Pan;
            string surface = o?.Surface;
            if (pr.Timbre == "step")
            {
                stepTurn++;
                if (pan == null) pan = SoundRules.StepPan(stepTurn); // left, right, left ...
                if (string.IsNullOrEmpty(surface)) surface = Surface;
            }
            double delay = double.IsNaN(delayMs) ? 0 : Math.Max(0, delayMs);
            if (double.IsInfinity(delay))
            {
                errors++; // js: start(Infinity) throws inside the voice: errors++, false
                return false;
            }
            return Schedule(SfxVariant.OfMeta(name, pr.Timbre, turn, mult, pan, surface, o?.PitchMul), delay / 1000, 0);
        }

        /// <summary>
        /// js: vendor/ds/bundle.js:826 S.play(name, delayMs, mult): silent when muted or before unlock; <c>hover</c> at most once per
        /// 50 ms (a first hover at time 0 does not count, as the JS writes it); <c>turn++</c>; paper and wood alternate by it.
        /// A negative delay starts the sound that far into itself (the browser starts it at once with its envelope already
        /// under way).
        /// </summary>
        private void BasePlay(string name, SoundPreset pr, double delayMs, double? mult)
        {
            if (state.Muted || !unlocked) return;
            double now = RealNow;
            if (name == "hover")
            {
                if (SoundRules.DsHoverThrottled(dsLastHover, now)) return;
                dsLastHover = now;
            }
            dsTurn++;
            double t = double.IsNaN(delayMs) ? 0 : delayMs / 1000;
            if (double.IsInfinity(t)) return; // js: a non-finite time throws inside SYN[timbre], which S.play swallows
            SfxVariant v = SfxVariant.OfBattle(name, pr.Timbre, dsTurn, mult);
            if (t >= 0) Schedule(v, t, 0);
            else Schedule(v, 0, -t);
        }

        private bool Schedule(SfxVariant v, double delaySeconds, double offsetSeconds)
        {
            AudioClip clip;
            try { clip = cache.Get(v); }
            catch (Exception)
            {
                errors++;
                return false;
            }
            if (!clip)
            {
                errors++; // js: a voice that throws is skipped (errors++)
                return false;
            }
            return player && player.Play(clip, DspNow + delaySeconds, offsetSeconds);
        }

        /// <summary>js: <c>audio.has(name)</c>: a battle or meta preset of that name exists.</summary>
        public bool Has(string name) => name != null && presets.ContainsKey(name);

        /// <summary>
        /// js: scenes/audio/Audio.js:797 stepTo({ fresh, line, miasmaDeath, result }): one step on the map and at most one result
        /// 280 ms later (miasma death &gt; a 20 % line &gt; the node's result). Returns the planned names. Both sounds are
        /// scheduled from the same DSP instant.
        /// </summary>
        public string[] StepTo(bool fresh = true, double line = 0, bool miasmaDeath = false, string result = null)
        {
            string[] plan = SoundRules.StepPlan(fresh, line, miasmaDeath, result);
            dspBase = AudioSettings.dspTime;
            try
            {
                Play(plan[0]);
                if (plan.Length > 1 && plan[1] == "miasmaLine") MiasmaLine(line, SoundData.ResultAfterStepMs);
                else if (plan.Length > 1) Play(plan[1], SoundData.ResultAfterStepMs);
            }
            finally
            {
                dspBase = null;
            }
            return plan;
        }

        /// <summary>js: scenes/audio/Audio.js:805 miasmaLine(pct, delayMs): the swell for one 20 % line, a whole tone lower and 10 % louder per line.</summary>
        public bool MiasmaLine(double linePct, double delayMs = 0)
        {
            SoundCall c = SoundRules.MiasmaLineCall(linePct, double.IsNaN(delayMs) ? 0 : delayMs);
            return Play(c.Name, c.DelayMs, c.Mult, new PlayOptions { PitchMul = c.PitchMul });
        }

        // ---- ambience (Audio.js:739-751, 811-814) -------------------------------------------------------------------

        /// <summary>
        /// js: scenes/audio/Audio.js:739 setAmbience(id, fadeMs = 1800): crossfades to "title", "hall", "interlude", "layer1".."layer7"
        /// (or 1..7); null, "" or "none" fade to silence; false for an unknown id. The footstep ground follows the place
        /// (its 12 step clips move to the front of the render queue).
        /// </summary>
        public bool SetAmbience(object id, double? fadeMs = null)
        {
            AmbienceEngine e = Engine;
            string before = e.Surface;
            bool ok = e.SetAmbience(id, fadeMs);
            if (ok && !destroyed && e.Surface != before) cache.Enqueue(SfxClipCache.StepSet(e.Surface), top: true);
            return ok;
        }

        /// <summary>js: scenes/audio/Audio.js:811 duckAmbience(factor, fadeMs = 600): lowers the ambience (not persisted). factor 0..1.</summary>
        public void DuckAmbience(double factor, double? fadeMs = null) => Engine.DuckAmbience(factor, fadeMs);

        /// <summary>The router's battle duck: <c>duckAmbience(BATTLE_DUCK = 0.4)</c> on, <c>duckAmbience(1)</c> off (main.js:423, 433).</summary>
        public void Duck(bool on, double? fadeMs = null) => DuckAmbience(on ? SoundData.BattleDuck : 1, fadeMs);

        // ---- settings (Audio.js:815-833) ----------------------------------------------------------------------------

        /// <summary>js: scenes/audio/Audio.js:815 setMaster(v).</summary>
        public void SetMaster(double v)
        {
            state.Master = SoundRules.Clamp01(v, state.Master);
            if (!state.Muted && bus) bus.SetTarget(state.Master, false);
            Changed();
        }

        /// <summary>js: scenes/audio/Audio.js:816 setEffects(v): the one-shots' volume (the battle synth's master is master × effects).</summary>
        public void SetEffects(double v)
        {
            state.Effects = SoundRules.Clamp01(v, state.Effects);
            if (player) player.SetVolume((float)state.Effects);
            Changed();
        }

        /// <summary>js: scenes/audio/Audio.js:817 setAmbienceVolume(v).</summary>
        public void SetAmbienceVolume(double v)
        {
            state.Ambience = SoundRules.Clamp01(v, state.Ambience);
            Engine.SetVolume(state.Ambience);
            Changed();
        }

        /// <summary>js: scenes/audio/Audio.js:818 setReverb(v): the damp room's send (every clip is rendered again).</summary>
        public void SetReverb(double v)
        {
            state.Reverb = SoundRules.Clamp01(v, state.Reverb);
            if (!destroyed) cache.SetReverb(state.Reverb);
            Changed();
        }

        /// <summary>js: scenes/audio/Audio.js:819 setMuted(v): the master fades to 0 and, 400 ms later, the ambience stops rendering.</summary>
        public void SetMuted(bool v)
        {
            state.Muted = v;
            ApplyMute();
            Changed();
        }

        /// <summary>js: scenes/audio/Audio.js:820 toggleMute(): returns the new state.</summary>
        public bool ToggleMute()
        {
            SetMuted(!state.Muted);
            return state.Muted;
        }

        /// <summary>
        /// js: scenes/audio/Audio.js:821 applySettings(partial): the given keys over the current ones, normalised (a non-finite
        /// volume reads as the module default), every volume ramped, the mute applied, the listeners told.
        /// </summary>
        public void ApplySettings(double? master = null, double? effects = null, double? ambience = null, double? reverb = null, bool? muted = null)
        {
            var merged = new Dictionary<string, object>
            {
                ["master"] = master ?? state.Master,
                ["effects"] = effects ?? state.Effects,
                ["ambience"] = ambience ?? state.Ambience,
                ["reverb"] = reverb ?? state.Reverb,
                ["muted"] = muted ?? state.Muted,
            };
            SoundSettings n = SoundRules.NormalizeSettings(merged);
            state.Master = n.Master;
            state.Effects = n.Effects;
            state.Ambience = n.Ambience;
            state.Reverb = n.Reverb;
            state.Muted = n.Muted;
            ApplyBase();
            if (!destroyed)
            {
                if (player) player.SetVolume((float)n.Effects);
                Engine.SetVolume(n.Ambience);
            }
            ApplyMute();
            Changed();
        }

        /// <summary>js: scenes/audio/Audio.js:830 onChange(fn): <paramref name="fn"/> gets the snapshot after every change; call the returned action to unsubscribe.</summary>
        public Action OnChange(Action<SoundSettings> fn)
        {
            if (fn == null) return () => { };
            listeners.Add(fn);
            return () => listeners.Remove(fn);
        }

        /// <summary>js: <c>audio.settings</c>: a snapshot <c>{ master, effects, ambience, reverb, muted }</c>.</summary>
        public SoundSettings Settings => Snapshot();
        /// <summary>js: <c>audio.muted</c> (setting it is setMuted).</summary>
        public bool Muted
        {
            get => state.Muted;
            set => SetMuted(value);
        }
        public double Master => state.Master;
        public double Effects => state.Effects;
        /// <summary>js: <c>audio.ambienceVolume</c>.</summary>
        public double AmbienceVolume => state.Ambience;
        public double Reverb => state.Reverb;
        public bool Unlocked => unlocked;
        /// <summary>js: <c>audio.ambience</c>: the place asked for last (null = silence).</summary>
        public string Ambience => Engine.Ambience;
        /// <summary>js: <c>audio.surface</c>: the ground the steps sound on.</summary>
        public string Surface => Engine.Surface;
        /// <summary>js: <c>audio.battleNames</c>.</summary>
        public string[] BattleNames => (string[])SoundData.DsSoundNames.Clone();
        /// <summary>js: <c>audio.metaNames</c>.</summary>
        public string[] MetaNames => (string[])SoundData.MetaSoundNames.Clone();
        /// <summary>js: <c>audio.timbres</c>: the timbres the meta synth implements.</summary>
        public string[] Timbres => (string[])MetaSynth.Timbres.Clone();
        /// <summary>js: <c>audio._errors</c>: voices that could not be built (sounds and ambience).</summary>
        public int Errors => errors + (Engine?.Errors ?? 0);
        public bool Destroyed => destroyed;

        // ---- lifetime (Audio.js:774-782, 835-841) -------------------------------------------------------------------

        /// <summary>
        /// js: scenes/audio/Audio.js:774 unlock(): the sounds may play; the pending place starts with a 2500 ms fade; the clips start
        /// rendering in the background (the buttons and the title first). Create calls it unless told not to.
        /// </summary>
        public bool Unlock()
        {
            if (destroyed) return false;
            unlocked = true;
            ApplyBase();
            if (!Ensure()) return false;
            Engine.Unlock();
            if (!bootQueued)
            {
                bootQueued = true;
                cache.Enqueue(SfxClipCache.BootSet(Surface));
            }
            return true;
        }

        /// <summary>js: scenes/audio/Audio.js:835 destroy(): the ambience stops, the clips are freed, the objects go away. Every play is silent after.</summary>
        public void Destroy() => Teardown(false);

        // The objects went away without Destroy (a scene unload, the end of play mode): free the rest, touch no object.
        private void OnHostDestroyed() => Teardown(true);

        private void Teardown(bool objectsGone)
        {
            if (destroyed) return;
            destroyed = true;
            freezeAt = null;
            Engine?.Destroy();
            if (player)
            {
                player.Frame -= Pump;
                player.Destroyed -= OnHostDestroyed;
                if (!objectsGone) player.StopAll();
            }
            cache.Dispose();
            if (!objectsGone)
            {
                if (bus && bus.gameObject != root) DestroyObject(bus);
                if (root) DestroyObject(root);
            }
            else if (bus && Application.isPlaying)
            {
                // a master stage left on a scene camera's listener that outlives us
                Object.Destroy(bus);
            }
            if (Current == this) Current = null;
        }

        private static void DestroyObject(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
