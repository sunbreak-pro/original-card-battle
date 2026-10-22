// An AudioParam with the Web Audio automation timeline, evaluated at every sample frame (a-rate):
//  - setValueAtTime holds until the next event;
//  - a linear / exponential ramp runs from the previous event's time and value to its own; an exponential ramp whose
//    start is 0 or of the other sign holds the start value until its time (spec);
//  - setTargetAtTime(v, T0, τ) is v + (V0 − v)·e^(−(t−T0)/τ) with V0 the value the earlier events give at T0;
//  - cancelScheduledValues(t) drops every event at or after t;
//  - an event of the same kind at the same time replaces the earlier one (Chromium's InsertEvent);
//  - setting .value records setValueAtTime(value, currentTime) as Chromium does; reading it gives the last rendered value;
//  - nodes connected into the param add their (mono) output to the timeline value, and the sum is clamped to the
//    param's nominal range.
using System;
using System.Collections.Generic;

namespace Trial.Audio.Synth
{
    public sealed class AudioParam
    {
        private enum EventKind
        {
            SetValue,
            LinearRamp,
            ExponentialRamp,
            SetTarget,
        }

        private sealed class ParamEvent
        {
            public EventKind Kind;
            public double Value, Time, TimeConstant;
            /// <summary>A ramp with no event before it starts at the time and value of its call (Chromium).</summary>
            public double CallTime, CallValue;
            public bool HasStart;
            public double Start;
            public bool IsRamp => Kind == EventKind.LinearRamp || Kind == EventKind.ExponentialRamp;
        }

        public AudioNode Owner { get; }
        public string Name { get; }
        public double DefaultValue { get; }
        public double MinValue { get; }
        public double MaxValue { get; }

        private double intrinsic, current;
        private readonly List<ParamEvent> events = new List<ParamEvent>();
        internal readonly List<AudioNode> Inputs = new List<AudioNode>();
        internal readonly float[] Values = new float[AudioContextLite.RenderQuantumFrames];
        private readonly float[] mono = new float[AudioContextLite.RenderQuantumFrames];

        internal AudioParam(AudioNode owner, string name, double defaultValue, double minValue, double maxValue)
        {
            Owner = owner;
            Name = name;
            DefaultValue = defaultValue;
            MinValue = minValue;
            MaxValue = maxValue;
            intrinsic = current = defaultValue;
        }

        /// <summary>js: <c>param.value</c>. Setting it is <c>setValueAtTime(v, currentTime)</c>; reading it gives the value the
        /// timeline gave at the end of the last rendered quantum (or the last value set before any rendering).</summary>
        public double Value
        {
            get => current;
            set
            {
                Check(value, "value");
                Owner.Context.Log?.Record(Owner, Name, "value", value);
                intrinsic = current = value;
                Insert(new ParamEvent { Kind = EventKind.SetValue, Value = value, Time = Owner.Context.CurrentTime });
            }
        }

        /// <summary>The timeline value at the end of the last rendered quantum (without the connected inputs).</summary>
        public double ComputedValue => current;

        public int EventCount => events.Count;

        // ---- automation ---------------------------------------------------------------------------------------------

        public AudioParam SetValueAtTime(double value, double startTime)
        {
            Check(value, "setValueAtTime"); CheckTime(startTime, "setValueAtTime");
            Owner.Context.Log?.Record(Owner, Name, "set", value, startTime);
            Insert(new ParamEvent { Kind = EventKind.SetValue, Value = value, Time = startTime });
            return this;
        }

        public AudioParam LinearRampToValueAtTime(double value, double endTime)
        {
            Check(value, "linearRampToValueAtTime"); CheckTime(endTime, "linearRampToValueAtTime");
            Owner.Context.Log?.Record(Owner, Name, "lin", value, endTime);
            Insert(new ParamEvent { Kind = EventKind.LinearRamp, Value = value, Time = endTime, CallTime = Owner.Context.CurrentTime, CallValue = current });
            return this;
        }

        public AudioParam ExponentialRampToValueAtTime(double value, double endTime)
        {
            Check(value, "exponentialRampToValueAtTime"); CheckTime(endTime, "exponentialRampToValueAtTime");
            if (value == 0) throw new ArgumentOutOfRangeException(nameof(value), Owner.Kind + "." + Name + ": exponential ramp to 0");
            Owner.Context.Log?.Record(Owner, Name, "exp", value, endTime);
            Insert(new ParamEvent { Kind = EventKind.ExponentialRamp, Value = value, Time = endTime, CallTime = Owner.Context.CurrentTime, CallValue = current });
            return this;
        }

        public AudioParam SetTargetAtTime(double target, double startTime, double timeConstant)
        {
            Check(target, "setTargetAtTime"); CheckTime(startTime, "setTargetAtTime");
            if (double.IsNaN(timeConstant) || double.IsInfinity(timeConstant) || timeConstant < 0)
                throw new ArgumentOutOfRangeException(nameof(timeConstant), "bad timeConstant " + timeConstant);
            Owner.Context.Log?.Record(Owner, Name, "target", target, startTime, timeConstant);
            Insert(new ParamEvent { Kind = EventKind.SetTarget, Value = target, Time = startTime, TimeConstant = timeConstant });
            return this;
        }

        public AudioParam CancelScheduledValues(double cancelTime)
        {
            CheckTime(cancelTime, "cancelScheduledValues");
            Owner.Context.Log?.Record(Owner, Name, "cancel", cancelTime);
            // The events left are all earlier: what they cached does not depend on the ones removed.
            events.RemoveAll(e => e.Time >= cancelTime);
            return this;
        }

        private void Check(double v, string what)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentException(Owner.Kind + "." + Name + "." + what + ": non-finite " + v);
        }

        private void CheckTime(double t, string what)
        {
            if (double.IsNaN(t) || double.IsInfinity(t) || t < 0) throw new ArgumentOutOfRangeException(nameof(t), Owner.Kind + "." + Name + "." + what + ": bad time " + t);
        }

        private void Insert(ParamEvent ev)
        {
            int i = 0;
            for (; i < events.Count; i++)
            {
                // Overwrite the same event type at the same time (Chromium).
                if (events[i].Time == ev.Time && events[i].Kind == ev.Kind)
                {
                    events[i] = ev;
                    InvalidateFrom(i);
                    return;
                }
                if (events[i].Time > ev.Time) break;
            }
            events.Insert(i, ev);
            InvalidateFrom(i);
        }

        /// <summary>A setTarget's cached start depends only on the events before it.</summary>
        private void InvalidateFrom(int index)
        {
            for (int i = index; i < events.Count; i++) events[i].HasStart = false;
        }

        // ---- evaluation ---------------------------------------------------------------------------------------------

        /// <summary>The timeline value at <paramref name="time"/> (without the connected inputs), clamped to the nominal range.</summary>
        public double ValueAtTime(double time) => Clamp(ValueAt(time, events.Count));

        private double Clamp(double v) => v < MinValue ? MinValue : v > MaxValue ? MaxValue : v;

        /// <summary>The value at <paramref name="t"/> from the first <paramref name="limit"/> events.</summary>
        private double ValueAt(double t, int limit)
        {
            if (limit == 0) return intrinsic;
            int i = LastAtOrBefore(t, limit);
            if (i < 0)
            {
                ParamEvent first = events[0];
                if (first.IsRamp && t >= first.CallTime && first.Time > first.CallTime)
                    return Ramp(first.Kind, first.CallTime, first.CallValue, first.Time, first.Value, t);
                return intrinsic;
            }
            ParamEvent e = events[i];
            if (i + 1 < limit)
            {
                ParamEvent next = events[i + 1];
                if (next.IsRamp && t < next.Time) return Ramp(next.Kind, e.Time, EndValue(i), next.Time, next.Value, t);
            }
            if (e.Kind == EventKind.SetTarget)
            {
                if (e.TimeConstant == 0) return e.Value;
                return e.Value + (StartOf(i) - e.Value) * Math.Exp(-(t - e.Time) / e.TimeConstant);
            }
            return e.Value;
        }

        private int LastAtOrBefore(double t, int limit)
        {
            int lo = 0, hi = limit - 1, found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (events[mid].Time <= t) { found = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return found;
        }

        /// <summary>The value an event leaves at its own time (the start of a ramp that follows it).</summary>
        private double EndValue(int i)
        {
            ParamEvent e = events[i];
            return e.Kind == EventKind.SetTarget ? StartOf(i) : e.Value;
        }

        /// <summary>A setTarget's V0: the value the events before it give at its time.</summary>
        private double StartOf(int i)
        {
            ParamEvent e = events[i];
            if (!e.HasStart)
            {
                e.Start = ValueAt(e.Time, i);
                e.HasStart = true;
            }
            return e.Start;
        }

        private static double Ramp(EventKind kind, double t0, double v0, double t1, double v1, double t)
        {
            if (t1 <= t0) return v1;
            double x = (t - t0) / (t1 - t0);
            if (kind == EventKind.LinearRamp) return v0 + (v1 - v0) * x;
            // Exponential: a start at 0 or of the other sign holds the start value until the ramp's time.
            if (v0 == 0 || (v0 < 0) != (v1 < 0)) return v0;
            return v0 * Math.Pow(v1 / v0, x);
        }

        /// <summary>
        /// Fills <see cref="Values"/> for the quantum that starts at <paramref name="startFrame"/>: the timeline at every frame
        /// plus the connected inputs, clamped. Returns true when every value of the quantum is the same.
        /// </summary>
        internal bool Fill(long startFrame, long quantum)
        {
            int n = AudioContextLite.RenderQuantumFrames;
            double sr = Owner.Context.SampleRate;
            bool constant;
            if (events.Count > 1)
            {
                // Drop the events the quantum can no longer reach (a live graph keeps adding them): from here on only the
                // last event at or before the quantum matters, and a setTarget keeps its start cached.
                int i0 = LastAtOrBefore(startFrame / sr, events.Count);
                if (i0 > 0)
                {
                    if (events[i0].Kind == EventKind.SetTarget) StartOf(i0);
                    events.RemoveRange(0, i0);
                }
            }
            if (events.Count == 0)
            {
                Fill((float)Clamp(intrinsic), n);
                current = intrinsic;
                constant = true;
            }
            else if (HeldOver(startFrame / sr, (startFrame + n - 1) / sr, out double held))
            {
                Fill((float)Clamp(held), n);
                current = held;
                constant = true;
            }
            else
            {
                double last = 0;
                for (int k = 0; k < n; k++)
                {
                    last = ValueAt((startFrame + k) / sr, events.Count);
                    Values[k] = (float)Clamp(last);
                }
                current = last;
                constant = false;
            }
            if (Inputs.Count > 0)
            {
                for (int i = 0; i < Inputs.Count; i++)
                {
                    AudioNode input = Inputs[i];
                    input.Pull(quantum);
                    float[] l = input.Out[0];
                    if (input.OutChannels == 1) for (int k = 0; k < n; k++) mono[k] = l[k];
                    else
                    {
                        float[] r = input.Out[1];
                        for (int k = 0; k < n; k++) mono[k] = 0.5f * (l[k] + r[k]);
                    }
                    for (int k = 0; k < n; k++) Values[k] += mono[k];
                }
                float lo = (float)MinValue, hi = (float)MaxValue;
                for (int k = 0; k < n; k++)
                {
                    float v = Values[k];
                    Values[k] = v < lo ? lo : v > hi ? hi : v;
                }
                long nextFrame = startFrame + n;
                for (int i = Inputs.Count - 1; i >= 0; i--)
                    if (Inputs[i].IsFinished(nextFrame)) AudioNode.Unlink(Inputs[i], this);
                constant = false;
            }
            return constant;
        }

        private void Fill(float v, int n)
        {
            for (int k = 0; k < n; k++) Values[k] = v;
        }

        /// <summary>True when the timeline is constant over [t0, t1]: before every event, or after the last change.</summary>
        private bool HeldOver(double t0, double t1, out double value)
        {
            value = 0;
            int i0 = LastAtOrBefore(t0, events.Count);
            int i1 = LastAtOrBefore(t1, events.Count);
            if (i0 != i1) return false;
            if (i0 < 0)
            {
                ParamEvent first = events[0];
                if (first.IsRamp && t1 >= first.CallTime) return false;
                value = intrinsic;
                return true;
            }
            ParamEvent e = events[i0];
            if (e.Kind == EventKind.SetTarget) return false;
            if (i0 + 1 < events.Count && events[i0 + 1].IsRamp) return false;
            value = e.Value;
            return true;
        }
    }
}
