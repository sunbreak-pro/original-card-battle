// Drives TrialClocks from the Unity frame loop: every frame, each clock steps with the unscaled frame time in ms
// (the browser's requestAnimationFrame), and the microtask queue is drained after each step and once more at the end
// (the browser's microtask checkpoint after every rAF callback). The clock itself is Trial.Async's TrialClock.
//
// A clock added during a frame takes its first step with the time since it was added (stage.js createClock reads
// performance.now() when created), not with the whole frame. A clock that was stopped (Alive false) is dropped.
// Input handlers drain the queue themselves (KeyStack does it after every handler; pointer handlers call AfterInput).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trial.Kit
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ClockDriver : MonoBehaviour
    {
        private sealed class Entry
        {
            public TrialClock Clock;
            public double AddedAt;
            public bool Fresh;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>The ClockDriver on host, added when missing.</summary>
        public static ClockDriver Ensure(GameObject host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            ClockDriver d = host.GetComponent<ClockDriver>();
            return d ? d : host.AddComponent<ClockDriver>();
        }

        /// <summary>Number of clocks driven.</summary>
        public int Count => _entries.Count;

        /// <summary>Steps `clock` every frame from now on; Dispose (or clock.Stop) ends it.</summary>
        public IDisposable Drive(TrialClock clock)
        {
            Add(clock);
            return new Handle(this, clock);
        }

        public void Add(TrialClock clock)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            for (int i = 0; i < _entries.Count; i++) if (ReferenceEquals(_entries[i].Clock, clock)) return;
            _entries.Add(new Entry { Clock = clock, AddedAt = Time.realtimeSinceStartupAsDouble, Fresh = true });
        }

        public void Remove(TrialClock clock)
        {
            for (int i = _entries.Count - 1; i >= 0; i--) if (ReferenceEquals(_entries[i].Clock, clock)) _entries.RemoveAt(i);
        }

        /// <summary>
        /// A new clock (stage.js createClock / meta sceneClock) driven by this driver. Scene time follows real time
        /// (clampSceneStep false), as stage.js's clock does; only the dt handed to OnFrame is clamped to 50 ms.
        /// </summary>
        public TrialClock CreateClock(Func<float> scale = null)
        {
            var c = new TrialClock(scale, null, clampSceneStep: false);
            Add(c);
            return c;
        }

        /// <summary>Drains the microtask queue (call after a pointer or UI handler ran: continuations it resolved run now).</summary>
        public static void AfterInput() => Microtasks.Drain();

        private void Update()
        {
            float frameMs = Time.unscaledDeltaTime * 1000f;
            double now = Time.realtimeSinceStartupAsDouble;
            // Copy: a step may add or stop clocks.
            Entry[] list = _entries.ToArray();
            foreach (Entry e in list)
            {
                if (!e.Clock.Alive) { _entries.Remove(e); continue; }
                float dt = frameMs;
                if (e.Fresh)
                {
                    e.Fresh = false;
                    dt = (float)Math.Max(0.0, Math.Min(frameMs, (now - e.AddedAt) * 1000.0));
                }
                try { e.Clock.Step(dt); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            Microtasks.Drain();
            _entries.RemoveAll(e => !e.Clock.Alive);
        }

        private sealed class Handle : IDisposable
        {
            private ClockDriver _driver;
            private TrialClock _clock;
            public Handle(ClockDriver d, TrialClock c) { _driver = d; _clock = c; }
            public void Dispose()
            {
                if (_driver) _driver.Remove(_clock);
                _driver = null;
                _clock = null;
            }
        }
    }
}
