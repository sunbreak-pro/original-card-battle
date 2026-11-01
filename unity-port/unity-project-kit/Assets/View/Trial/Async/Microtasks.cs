// The microtask queue: where every Promise continuation runs, as in the browser.
//
// A continuation never runs inside the call that settles its promise. It is posted here and runs when the
// queue is drained: after each clock step (TrialClock.Step drains at its end) and after each input handler.
// That keeps the order of beats the JS game had: everything a frame resolves runs after the frame's own
// work, in the order it was resolved.
//
// Main thread only. Statics are reset by Reset() (Unity runs with domain reload off).
using System;
using System.Collections.Generic;

namespace Trial
{
    public static class Microtasks
    {
        private static readonly Queue<Action> Queue = new Queue<Action>();
        private static bool _draining;

        /// <summary>A rejected promise nobody handled, or an exception thrown by a posted action.</summary>
        public static Action<Exception> Unhandled = e => Console.Error.WriteLine(e);

        /// <summary>How many actions are waiting.</summary>
        public static int Pending => Queue.Count;

        public static void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Queue.Enqueue(action);
        }

        /// <summary>Runs posted actions until the queue is empty (actions posted meanwhile run too). Re-entrant calls return at once.</summary>
        public static void Drain()
        {
            if (_draining) return;
            _draining = true;
            try
            {
                int guard = 0;
                while (Queue.Count > 0)
                {
                    Action a = Queue.Dequeue();
                    try { a(); }
                    catch (Exception e) { Report(e); }
                    if (++guard > 1_000_000)
                    {
                        Report(new InvalidOperationException("Microtasks.Drain: more than a million actions in one drain (a promise loop?)"));
                        Queue.Clear();
                        break;
                    }
                }
            }
            finally { _draining = false; }
        }

        /// <summary>Drops everything waiting (a new Play session, a test's set-up).</summary>
        public static void Reset()
        {
            Queue.Clear();
            _draining = false;
        }

        internal static void Report(Exception e)
        {
            try { Unhandled?.Invoke(e); } catch { /* a broken logger must not stop the queue */ }
        }
    }
}
