// The miasma's breath for a screen whose reading moves (#728): the phase is carried from frame to frame, so
// a new reading only changes the pace and the breath never jumps. MiasmaLook.Breath reads the phase off the
// absolute clock, which holds for one reading only.
namespace Backdrop
{
    public sealed class MiasmaBreath
    {
        /// <summary>The clock moves the breath at most this much in one call, so a pause (a battle on top) resumes it where it stopped.</summary>
        public const double MaxStepMs = 100.0;

        private double _phase;
        private double _at = -1.0;
        private float _pace = MiasmaLook.BreathFrom;

        /// <summary>
        /// Moves the breath on to <paramref name="nowMs"/> and returns it (0 at rest, 1 at the bottom of a
        /// breath). A breath starts from rest when <paramref name="percent"/> reaches 80%, and one under way
        /// when it falls below 80% finishes at its last pace instead of snapping back.
        /// </summary>
        public float Advance(double nowMs, float percent)
        {
            double elapsed = _at < 0.0 ? 0.0 : System.Math.Min(System.Math.Max(nowMs - _at, 0.0), MaxStepMs);
            _at = nowMs;
            bool breathing = percent >= MiasmaLook.BreathFrom;
            if (breathing) _pace = percent;
            else if (_phase <= 0.0) return 0f;

            // One step is far shorter than the fastest period, so a smaller phase means a cycle just ended.
            double next = MiasmaLook.AdvanceBreath(_phase, elapsed, _pace);
            _phase = !breathing && next < _phase ? 0.0 : next;
            return MiasmaLook.BreathAtPhase(_phase);
        }
    }
}
